using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Kernel.Contracts.Models;

public class PermissionCheckResult
{
    public bool IsAllowed { get; set; }
    public string? DenyReason { get; set; }
    public string? RuleCode { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public static PermissionCheckResult Allow() =>
        new() { IsAllowed = true, RiskLevel = RiskLevel.Low };

    public static PermissionCheckResult Deny(string reason, RiskLevel risk) =>
        new() { IsAllowed = false, DenyReason = reason, RiskLevel = risk };

    public static PermissionCheckResult Block(string ruleCode, string reason) =>
        new()
        {
            IsAllowed = false,
            RuleCode = ruleCode,
            DenyReason = $"[安全红线 {ruleCode}] {reason}",
            RiskLevel = RiskLevel.Critical
        };
}
