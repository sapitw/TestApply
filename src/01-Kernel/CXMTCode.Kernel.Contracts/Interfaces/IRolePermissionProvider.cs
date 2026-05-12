using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>角色权限映射 - 硬编码，权限码与菜单</summary>
public interface IRolePermissionProvider
{
    IReadOnlySet<string> GetPermissionsForRole(UserRole role);
    bool HasPermission(UserRole role, string permissionCode);
    IReadOnlyList<MenuConfig> GetMenusForRole(UserRole role);
    IReadOnlySet<string> GetAllowedRoutes(UserRole role);
}
