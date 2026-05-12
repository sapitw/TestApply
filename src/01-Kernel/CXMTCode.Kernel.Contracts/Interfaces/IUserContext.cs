using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>用户上下文 - 从 JWT Token 解析的当前请求身份</summary>
public interface IUserContext
{
    Guid UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    UserRole Role { get; }
    string DepartmentCode { get; }
    string DepartmentName { get; }
    string ClientIp { get; }
    bool IsAuthenticated { get; }

    /// <summary>层级判断（高角色覆盖低角色）</summary>
    bool HasRole(UserRole requiredRole);
    bool IsDbaOrAbove { get; }
    bool IsSysAdmin { get; }
}
