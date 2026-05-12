using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Audit;

/// <summary>E1 操作留痕 - 所有受控操作落入 CXMT_AUDIT_LOGS</summary>
public class OperationTracePlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.OperationTrace";
    public override string DisplayName => "E1 操作留痕";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var opType   = input.GetParameter<string>("operationType") ?? "";
        var opDesc   = input.GetParameter<string>("operationDesc") ?? "";
        var sql      = input.GetParameter<string>("sql");
        var targetDb = input.GetParameter<string>("targetDb");
        var targetTb = input.GetParameter<string>("targetTable");

        var result = await AuditLogger.WriteAsync(
            CurrentUser.UserId, CurrentUser.UserName, CurrentUser.Role,
            opType, opDesc, targetDb, targetTb, sql,
            clientIp: CurrentUser.ClientIp);
        return PluginOutput.Ok(result);
    }
}

/// <summary>E2 哈希链校验 - 抽样验证审计日志完整性</summary>
public class HashChainVerifyPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.HashChainVerify";
    public override string DisplayName => "E2 审计哈希链校验";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var logId = input.GetParameter<long>("logId");
        var ok = await AuditLogger.VerifyIntegrityAsync(logId);
        return PluginOutput.Ok(new { LogId = logId, Verified = ok });
    }
}

/// <summary>E3 电子签名 - 关键审批节点附 SM2 签名（占位实现）</summary>
public class ElectronicSignaturePlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.ElectronicSignature";
    public override string DisplayName => "E3 电子签名";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var payload = input.GetParameter<string>("payload") ?? "";
        // TODO：从用户证书库获取私钥；当前仅返回 base64(payload + userId) 占位
        var stub = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{CurrentUser.UserId}:{payload}"));
        return Task.FromResult(PluginOutput.Ok(new { Signature = stub, Algorithm = "STUB-SM2" }));
    }
}

/// <summary>E4 合规报表导出</summary>
public class ComplianceReportPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.ComplianceReport";
    public override string DisplayName => "E4 合规报表导出";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var from = input.GetParameter<DateTime>("from");
        var to   = input.GetParameter<DateTime>("to");
        // TODO：生成 21CFR Part11 合规报告 PDF
        return Task.FromResult(PluginOutput.Ok(new { From = from, To = to, FilePath = "/tmp/report.pdf" }));
    }
}

/// <summary>E5 审计搜索</summary>
public class AuditSearchPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.Search";
    public override string DisplayName => "E5 审计搜索";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var from   = input.GetParameter<DateTime>("from");
        var to     = input.GetParameter<DateTime>("to");
        var op     = input.GetParameter<string>("operationType");
        var rows   = await AuditLogger.QueryAsync(null, from, to, op, 0, 200);
        return PluginOutput.Ok(rows);
    }
}

/// <summary>E6 数据脱敏 - 在导出前对敏感字段做脱敏</summary>
public class DataMaskingPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.DataMasking";
    public override string DisplayName => "E6 敏感数据脱敏";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var raw = input.GetParameter<string>("data") ?? "";
        // TODO：根据字段元信息脱敏，当前演示版仅对手机号/邮箱做简单替换
        var masked = System.Text.RegularExpressions.Regex.Replace(raw, "(1[3-9]\\d)\\d{4}(\\d{4})", "$1****$2");
        masked = System.Text.RegularExpressions.Regex.Replace(masked, "([\\w.])[\\w.]*(@\\w+\\.\\w+)", "$1***$2");
        return Task.FromResult(PluginOutput.Ok(new { Original = raw, Masked = masked }));
    }
}

/// <summary>E7 审批流程驱动 - 根据 CXMT_APPROVAL_INSTANCES 推进节点</summary>
public class ApprovalWorkflowPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Audit.ApprovalWorkflow";
    public override string DisplayName => "E7 审批流程驱动";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var instanceId = input.GetParameter<string>("instanceId") ?? "";
        var action     = input.GetParameter<string>("action") ?? "Approve";
        // TODO：基于 Workflow-Core 实现节点推进；当前仅返回占位
        return Task.FromResult(PluginOutput.Ok(new { InstanceId = instanceId, Action = action, Done = true }));
    }
}
