using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// B5 表权限校验插件 - 表级最小权限。
/// DBA/SysAdmin 默认可访问已准入表；普通用户必须有显式授权 + 操作类型许可。
/// </summary>
public class TablePermissionPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.TablePermission";
    public override string DisplayName => "B5 表权限校验";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var userId      = input.GetParameter<Guid>("userId");
        var userRole    = input.GetParameter<UserRole>("userRole");
        var dbName      = input.GetParameter<string>("databaseName") ?? "";
        var tableName   = input.GetParameter<string>("tableName") ?? "";
        var opType      = input.GetParameter<SqlOperationType>("operationType");
        var permissions = input.GetParameter<List<TablePermission>>("permissions") ?? new();

        var admitted = permissions.Any(p =>
            p.DatabaseName.Equals(dbName, StringComparison.OrdinalIgnoreCase) &&
            p.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
            p.IsAdmitted);
        if (!admitted)
            return Task.FromResult(Result(false, $"表 {dbName}.{tableName} 未完成准入", PermissionDenyReason.TableNotAdmitted));

        if (userRole >= UserRole.DBA)
            return Task.FromResult(Result(true, $"{userRole} 通过校验"));

        var userPerm = permissions.FirstOrDefault(p =>
            p.DatabaseName.Equals(dbName, StringComparison.OrdinalIgnoreCase) &&
            p.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
            p.AuthorizedUsers.Contains(userId));
        if (userPerm is null)
            return Task.FromResult(Result(false, "您未被授权此表", PermissionDenyReason.UserNotAuthorized));

        if (!userPerm.AllowedOperations.Contains(opType))
            return Task.FromResult(Result(false, $"无权执行 {opType}", PermissionDenyReason.OperationNotAllowed));

        return Task.FromResult(Result(true, "校验通过"));
    }

    private static PluginOutput Result(bool allowed, string msg, PermissionDenyReason? reason = null) =>
        PluginOutput.Ok(new PermissionValidationResult
        {
            IsAllowed = allowed,
            Message   = msg,
            Reason    = reason,
            RiskLevel = allowed ? RiskLevel.Low : RiskLevel.High
        });
}

public enum PermissionDenyReason { TableNotAdmitted, UserNotAuthorized, OperationNotAllowed }

public class TablePermission
{
    public string PermissionId { get; set; } = Guid.NewGuid().ToString("N");
    public string DatabaseName { get; set; } = "";
    public string TableName { get; set; } = "";
    public bool IsAdmitted { get; set; }
    public List<Guid> AuthorizedUsers { get; set; } = new();
    public List<SqlOperationType> AllowedOperations { get; set; } = new();
}

public class PermissionValidationResult
{
    public bool IsAllowed { get; set; }
    public string Message { get; set; } = "";
    public PermissionDenyReason? Reason { get; set; }
    public RiskLevel RiskLevel { get; set; }
}
