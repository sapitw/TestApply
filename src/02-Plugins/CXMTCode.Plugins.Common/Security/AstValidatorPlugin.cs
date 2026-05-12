using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// B2 AST 格式校验插件 - 在 PermissionChecker 之后做语义级深度校验。
/// 与 PermissionChecker 不同：PermissionChecker 是粗粒度红线，AST 校验是细粒度风险等级判断。
/// </summary>
public class AstValidatorPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.AstValidator";
    public override string DisplayName => "B2 AST 格式校验";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var sql      = input.GetParameter<string>("sql") ?? string.Empty;
        var userRole = input.GetParameter<UserRole>("userRole");
        var u = sql.Trim().ToUpperInvariant();
        var violations = new List<string>();

        if (u.StartsWith("UPDATE") && !u.Contains("WHERE")) violations.Add("[RL003] UPDATE 必须包含 WHERE");
        if (u.StartsWith("DELETE") && !u.Contains("WHERE")) violations.Add("[RL004] DELETE 必须包含 WHERE");
        if (CountOccurrences(u, "SELECT") > 1) violations.Add("禁止子查询嵌套");
        if (u.Contains(" UNION ")) violations.Add("禁止 UNION 操作");
        foreach (var fn in new[] { "SYS_CONTEXT", "USERENV", "DBMS_", "SYS." })
            if (u.Contains(fn)) violations.Add($"禁止系统函数 {fn}");
        if (u.Contains(" JOIN ")) violations.Add("禁止跨表 JOIN");

        // DDL 仅 DBA/SysAdmin 可执行
        if (userRole < UserRole.DBA)
        {
            foreach (var ddl in new[] { "CREATE", "ALTER", "DROP", "TRUNCATE" })
                if (u.StartsWith(ddl)) violations.Add($"角色 {userRole} 禁止 DDL: {ddl}");
        }

        return Task.FromResult(PluginOutput.Ok(new AstValidationResult
        {
            IsValid    = violations.Count == 0,
            Violations = violations,
            RiskLevel  = violations.Count == 0 ? RiskLevel.Low
                        : violations.Any(v => v.Contains("RL0")) ? RiskLevel.Critical
                        : RiskLevel.High
        }));
    }

    private static int CountOccurrences(string text, string sub)
    {
        int c = 0, i = 0;
        while ((i = text.IndexOf(sub, i, StringComparison.Ordinal)) != -1) { c++; i += sub.Length; }
        return c;
    }
}

public class AstValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Violations { get; set; } = new();
    public RiskLevel RiskLevel { get; set; }
}
