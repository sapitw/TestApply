using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// B3 动态白名单规则校验。
/// 普通用户：只有命中规则的表才可操作；DBA/SysAdmin 自动跳过。
/// </summary>
public class WhitelistValidatorPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.WhitelistValidator";
    public override string DisplayName => "B3 白名单规则校验";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var tableName = input.GetParameter<string>("tableName") ?? "";
        var rules     = input.GetParameter<List<WhitelistRule>>("rules") ?? new();
        var userRole  = input.GetParameter<UserRole>("userRole");

        if (userRole >= UserRole.DBA)
            return Task.FromResult(PluginOutput.Ok(new WhitelistValidationResult
            { IsAllowed = true, Message = "DBA 及以上跳过白名单" }));

        var match = rules.Where(r =>
            string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase) &&
            r.Status == WhitelistRuleStatus.Active).ToList();

        if (match.Count == 0)
            return Task.FromResult(PluginOutput.Ok(new WhitelistValidationResult
            {
                IsAllowed = false,
                Message   = $"表 {tableName} 未在白名单中",
                RiskLevel = RiskLevel.High
            }));

        // 时间窗口校验
        var now = DateTime.Now.TimeOfDay;
        var inWindow = match.Any(r => r.AllowedTimeWindows is null || r.AllowedTimeWindows.Count == 0 ||
            r.AllowedTimeWindows.Any(w => w.Start <= now && now <= w.End));

        return Task.FromResult(PluginOutput.Ok(new WhitelistValidationResult
        {
            IsAllowed     = inWindow,
            Message       = inWindow ? "命中白名单" : "不在允许时间窗口内",
            MatchedRules  = match.Select(r => r.RuleName).ToList(),
            RiskLevel     = inWindow ? RiskLevel.Low : RiskLevel.Medium
        }));
    }
}

public enum WhitelistRuleStatus { Disabled = 0, Active = 1 }

public class WhitelistRule
{
    public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
    public string RuleName { get; set; } = "";
    public string TableName { get; set; } = "";
    public DatabaseType DatabaseType { get; set; }
    public List<string>? AllowedColumns { get; set; }
    public int MaxAffectedRows { get; set; } = 1000;
    public List<TimeWindow>? AllowedTimeWindows { get; set; }
    public WhitelistRuleStatus Status { get; set; } = WhitelistRuleStatus.Active;
}

public class TimeWindow
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

public class WhitelistValidationResult
{
    public bool IsAllowed { get; set; }
    public string Message { get; set; } = "";
    public List<string> MatchedRules { get; set; } = new();
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
}
