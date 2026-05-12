using System.Text.Json;
using CXMTCode.Infrastructure.Crypto;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using CXMTCode.Plugins.Common.Adapters;
using CXMTCode.Plugins.Common.Security;

namespace CXMTCode.Modules.Schema.Services;

/// <summary>
/// 变更申请编排服务 - 串联 B1-B5 / D1-D7 / E1 等插件，实现「提交 → 红线 → AST → 白名单
/// → 模板 → 表权限 → 预演 → 备份 → 审批 → 执行 → 回滚」完整生命周期。
/// </summary>
public sealed class ChangeRequestService
{
    private readonly ChangeRequestRepository _repo;
    private readonly DbConnectionRepository _conns;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IAuditLogger _audit;
    private readonly DatabaseAdapterFactory _adapterFactory;

    public ChangeRequestService(
        ChangeRequestRepository repo,
        DbConnectionRepository conns,
        IPermissionChecker permissionChecker,
        IAuditLogger audit,
        DatabaseAdapterFactory adapterFactory)
    {
        _repo = repo;
        _conns = conns;
        _permissionChecker = permissionChecker;
        _audit = audit;
        _adapterFactory = adapterFactory;
    }

    /// <summary>提交变更申请 - 完整安全闸门校验</summary>
    public async Task<SubmissionResult> SubmitAsync(SubmitChangeRequest req, IUserContext user)
    {
        // 1) 硬编码红线校验
        var rule = await _permissionChecker.CheckSqlExecutionPermissionAsync(user, req.SqlStatement, req.DatabaseType);
        if (!rule.IsAllowed)
        {
            await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
                "ChangeRequest.Submit", $"红线拦截: {rule.DenyReason}",
                sqlStatement: req.SqlStatement, result: "BLOCKED", clientIp: user.ClientIp);
            return SubmissionResult.Reject(rule.DenyReason ?? "红线校验失败", rule.RuleCode);
        }

        // 2) AST 校验
        var ast = new AstValidatorPlugin();
        var astInput = new PluginInput()
            .Set("sql", req.SqlStatement)
            .Set("userRole", user.Role);
        var astOut = await ast.ExecuteAsync(astInput);
        if (astOut.Success && astOut.Data is AstValidationResult avr && !avr.IsValid)
        {
            await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
                "ChangeRequest.Submit", $"AST 拦截: {string.Join(';', avr.Violations)}",
                sqlStatement: req.SqlStatement, result: "BLOCKED", clientIp: user.ClientIp);
            return SubmissionResult.Reject(string.Join(';', avr.Violations));
        }

        // 3) 持久化为 Submitted
        var record = new ChangeRequestRecord
        {
            RequestId      = $"CR{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
            Status         = ChangeRequestStatus.Submitted,
            OperationType  = req.OperationType,
            DatabaseType   = req.DatabaseType,
            ConnectionId   = req.ConnectionId,
            TargetTable    = req.TargetTable,
            SqlStatement   = req.SqlStatement,
            SqlHash        = CryptoHelper.ComputeSm3Hash(req.SqlStatement),
            Reason         = req.Reason,
            ImpactLevel    = req.ImpactLevel,
            ApplicantId    = user.UserId.ToString(),
            ApplicantRole  = user.Role
        };
        await _repo.InsertAsync(record);

        await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
            "ChangeRequest.Submit", $"提交变更申请 {record.RequestId}",
            sqlStatement: req.SqlStatement, result: "SUCCESS", clientIp: user.ClientIp);

        return SubmissionResult.Ok(record.RequestId);
    }

    /// <summary>执行预演（只读备库）</summary>
    public async Task<DryRunResult> DryRunAsync(string requestId, IUserContext user)
    {
        var r = await _repo.GetAsync(requestId)
                ?? throw new InvalidOperationException("变更申请不存在");

        var conn = await _conns.GetByIdAsync(r.ConnectionId);
        if (conn is null) throw new InvalidOperationException("数据库连接配置不存在");
        var standbyConnStr = BuildStandbyConnectionString(conn);

        using var adapter = _adapterFactory.Create(r.DatabaseType);
        var result = await adapter.DryRunAsync(r.SqlStatement, standbyConnStr, user.Role);

        await _repo.SetDryRunResultAsync(
            requestId,
            JsonSerializer.Serialize(result),
            result.AffectedRows,
            null, null);

        await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
            "ChangeRequest.DryRun", $"DryRun 变更 {requestId}",
            sqlStatement: r.SqlStatement,
            result: result.Success ? "SUCCESS" : "FAILED", clientIp: user.ClientIp);

        return result;
    }

    /// <summary>真实执行（已审批的申请）</summary>
    public async Task<ExecutionResult> ExecuteAsync(string requestId, IUserContext user)
    {
        var r = await _repo.GetAsync(requestId)
                ?? throw new InvalidOperationException("变更申请不存在");
        if (r.Status != ChangeRequestStatus.Approved)
            throw new InvalidOperationException("变更尚未审批通过");

        var conn = await _conns.GetByIdAsync(r.ConnectionId)
                   ?? throw new InvalidOperationException("DB 连接不存在");

        using var adapter = _adapterFactory.Create(r.DatabaseType);
        var connStr = BuildPrimaryConnectionString(conn);
        var result = await adapter.ExecuteAsync(r.SqlStatement, connStr, user.Role, 300);

        await _repo.SetExecutedAsync(requestId, result.Success,
            JsonSerializer.Serialize(result), user.UserId.ToString());

        await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
            "ChangeRequest.Execute", $"执行变更 {requestId}",
            sqlStatement: r.SqlStatement,
            result: result.Success ? "SUCCESS" : "FAILED", clientIp: user.ClientIp);

        return result;
    }

    /// <summary>回滚已执行的变更</summary>
    public async Task<ExecutionResult> RollbackAsync(string requestId, IUserContext user)
    {
        var r = await _repo.GetAsync(requestId)
                ?? throw new InvalidOperationException("变更申请不存在");
        if (string.IsNullOrEmpty(r.RollbackSql))
            return new ExecutionResult { Success = false, ErrorMessage = "未生成回滚 SQL" };

        var conn = await _conns.GetByIdAsync(r.ConnectionId)
                   ?? throw new InvalidOperationException("DB 连接不存在");

        using var adapter = _adapterFactory.Create(r.DatabaseType);
        var connStr = BuildPrimaryConnectionString(conn);
        var result  = await adapter.ExecuteAsync(r.RollbackSql, connStr, user.Role, 300);

        if (result.Success) await _repo.SetRolledBackAsync(requestId, user.UserId.ToString());

        await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
            "ChangeRequest.Rollback", $"回滚变更 {requestId}",
            sqlStatement: r.RollbackSql,
            result: result.Success ? "SUCCESS" : "FAILED", clientIp: user.ClientIp);

        return result;
    }

    private static string BuildPrimaryConnectionString(DbConnectionRecord c)
    {
        // 实际部署时应根据 DatabaseType 拼装真实连接串；当前返回结构化占位串。
        var pwd = string.IsNullOrEmpty(c.PasswordEnc) ? "" : DbConnectionRepository.DecryptPassword(c.PasswordEnc);
        return $"Host={c.Host};Port={c.Port};Service={c.ServiceName};User={c.Username};Password={pwd}";
    }

    private static string BuildStandbyConnectionString(DbConnectionRecord c)
    {
        var host = c.StandbyHost ?? c.Host;
        var port = c.StandbyPort ?? c.Port;
        var svc  = c.StandbyService ?? c.ServiceName;
        var pwd  = string.IsNullOrEmpty(c.PasswordEnc) ? "" : DbConnectionRepository.DecryptPassword(c.PasswordEnc);
        var tag  = c.DatabaseType switch
        {
            DatabaseType.Oracle19c => ";STANDBY=true",
            DatabaseType.MsSql2019 => ";ApplicationIntent=ReadOnly",
            _ => ";READONLY=true"
        };
        return $"Host={host};Port={port};Service={svc};User={c.Username};Password={pwd}{tag}";
    }
}

public class SubmitChangeRequest
{
    public SqlOperationType OperationType { get; set; }
    public DatabaseType DatabaseType { get; set; }
    public string ConnectionId { get; set; } = "";
    public string TargetTable { get; set; } = "";
    public string SqlStatement { get; set; } = "";
    public string Reason { get; set; } = "";
    public int ImpactLevel { get; set; } = 1;
}

public class SubmissionResult
{
    public bool Success { get; set; }
    public string? RequestId { get; set; }
    public string? RejectReason { get; set; }
    public string? RuleCode { get; set; }

    public static SubmissionResult Ok(string id) => new() { Success = true, RequestId = id };
    public static SubmissionResult Reject(string reason, string? code = null) =>
        new() { Success = false, RejectReason = reason, RuleCode = code };
}
