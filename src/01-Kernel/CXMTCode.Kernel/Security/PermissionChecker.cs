using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Security;

/// <summary>
/// 权限校验中枢 - 硬编码 10 条安全红线 (RL001~RL010)。
/// 红线不可修改 / 不可关闭 / 不可绕过。
/// </summary>
public sealed class PermissionChecker : IPermissionChecker
{
    private readonly IAuditLogger _audit;

    public PermissionChecker(IAuditLogger audit) => _audit = audit;

    public Task<PermissionCheckResult> CheckRoleAsync(IUserContext user, UserRole requiredRole)
    {
        if (user is null || !user.IsAuthenticated)
            return Task.FromResult(PermissionCheckResult.Deny("用户未认证", RiskLevel.Critical));
        return Task.FromResult(user.HasRole(requiredRole)
            ? PermissionCheckResult.Allow()
            : PermissionCheckResult.Deny($"角色不足：当前 {user.Role}，需要 {requiredRole}", RiskLevel.High));
    }

    public Task<PermissionCheckResult> CheckSqlExecutionPermissionAsync(
        IUserContext user, string sql, DatabaseType dbType)
    {
        if (user is null || !user.IsAuthenticated)
            return Task.FromResult(PermissionCheckResult.Deny("未认证", RiskLevel.Critical));

        var upper = (sql ?? string.Empty).Trim().ToUpperInvariant();

        // RL001 DROP
        if (StartsWithKeyword(upper, "DROP")) return Block("RL001", "DROP 操作被安全红线禁止");
        // RL002 TRUNCATE
        if (StartsWithKeyword(upper, "TRUNCATE")) return Block("RL002", "TRUNCATE 操作被安全红线禁止");
        // RL003 无 WHERE 的 UPDATE
        if (StartsWithKeyword(upper, "UPDATE") && !ContainsWord(upper, "WHERE"))
            return Block("RL003", "UPDATE 必须带 WHERE 条件");
        // RL004 无 WHERE 的 DELETE
        if (StartsWithKeyword(upper, "DELETE") && !ContainsWord(upper, "WHERE"))
            return Block("RL004", "DELETE 必须带 WHERE 条件");
        // RL005 审计日志表
        if (upper.Contains("CXMT_AUDIT") || upper.Contains("AUDIT_LOG"))
            return Block("RL005", "审计表禁止直接访问");
        // RL006 GRANT / REVOKE
        if (StartsWithKeyword(upper, "GRANT") || StartsWithKeyword(upper, "REVOKE"))
            return Block("RL006", "权限操作被禁止");
        // RL007 CREATE USER
        if (upper.StartsWith("CREATE USER"))
            return Block("RL007", "禁止创建数据库用户");
        // RL008 ALTER SYSTEM
        if (upper.StartsWith("ALTER SYSTEM"))
            return Block("RL008", "系统级操作被禁止");
        // RL009 多行注释包裹（无 WHERE 时可疑）
        if (upper.Contains("/*") && upper.Contains("*/") && !ContainsWord(upper, "WHERE"))
            return Block("RL009", "可疑注释使用，疑似注入");
        // RL010 UNION
        if (ContainsWord(upper, "UNION"))
            return Block("RL010", "UNION 操作被禁止");

        return Task.FromResult(PermissionCheckResult.Allow());
    }

    public Task<PermissionCheckResult> CheckTablePermissionAsync(
        IUserContext user, string dbName, string tableName, SqlOperationType opType)
    {
        if (user is null || !user.IsAuthenticated)
            return Task.FromResult(PermissionCheckResult.Deny("未认证", RiskLevel.Critical));
        // DBA 及以上默认放行已准入表（详细校验由 B5 TablePermissionPlugin 完成）
        if (user.IsDbaOrAbove) return Task.FromResult(PermissionCheckResult.Allow());
        return Task.FromResult(PermissionCheckResult.Allow());
    }

    public Task<PermissionCheckResult> CheckOwnershipAsync(IUserContext user, Guid dataOwnerId)
    {
        if (user is null || !user.IsAuthenticated)
            return Task.FromResult(PermissionCheckResult.Deny("未认证", RiskLevel.Critical));
        if (user.IsDbaOrAbove) return Task.FromResult(PermissionCheckResult.Allow());
        return Task.FromResult(user.UserId == dataOwnerId
            ? PermissionCheckResult.Allow()
            : PermissionCheckResult.Deny("无权查看他人数据", RiskLevel.Medium));
    }

    private static bool StartsWithKeyword(string upper, string keyword) =>
        upper.StartsWith(keyword + " ") || upper.StartsWith(keyword + "\t") || upper == keyword;

    private static bool ContainsWord(string text, string word)
    {
        var idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            bool boundaryLeft  = idx == 0               || !char.IsLetterOrDigit(text[idx - 1]);
            bool boundaryRight = idx + word.Length == text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
            if (boundaryLeft && boundaryRight) return true;
            idx += word.Length;
        }
        return false;
    }

    private static Task<PermissionCheckResult> Block(string code, string reason) =>
        Task.FromResult(PermissionCheckResult.Block(code, reason));
}
