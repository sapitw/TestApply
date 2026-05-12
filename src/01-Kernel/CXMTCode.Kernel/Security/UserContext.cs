using System.Security.Claims;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;

namespace CXMTCode.Kernel.Security;

/// <summary>用户上下文实现 - 从 JWT ClaimsPrincipal 解析</summary>
public sealed class UserContext : IUserContext
{
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.User;
    public string DepartmentCode { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string ClientIp { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; }

    public bool IsDbaOrAbove => (int)Role >= (int)UserRole.DBA;
    public bool IsSysAdmin => Role == UserRole.SysAdmin;
    public bool HasRole(UserRole requiredRole) => (int)Role >= (int)requiredRole;

    public UserContext() { }

    public static UserContext Anonymous(string clientIp = "") =>
        new() { IsAuthenticated = false, Role = UserRole.User, ClientIp = clientIp };

    public static UserContext FromClaimsPrincipal(ClaimsPrincipal? principal, string clientIp)
    {
        var ctx = new UserContext { ClientIp = clientIp };
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return ctx;
        }

        ctx.IsAuthenticated = true;

        var uid = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;
        if (uid != null && Guid.TryParse(uid, out var gid)) ctx.UserId = gid;

        ctx.UserName = principal.FindFirst(ClaimTypes.Name)?.Value
                       ?? principal.FindFirst("unique_name")?.Value
                       ?? string.Empty;
        ctx.DisplayName = principal.FindFirst(ClaimTypes.GivenName)?.Value
                          ?? principal.FindFirst("display_name")?.Value
                          ?? ctx.UserName;

        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
                        ?? principal.FindFirst("role")?.Value;
        if (!string.IsNullOrEmpty(roleClaim))
        {
            if (Enum.TryParse<UserRole>(roleClaim, true, out var r)) ctx.Role = r;
            else if (int.TryParse(roleClaim, out var ri) && Enum.IsDefined(typeof(UserRole), ri))
                ctx.Role = (UserRole)ri;
        }

        ctx.DepartmentCode = principal.FindFirst("dept_code")?.Value ?? string.Empty;
        ctx.DepartmentName = principal.FindFirst("dept_name")?.Value ?? string.Empty;
        return ctx;
    }
}
