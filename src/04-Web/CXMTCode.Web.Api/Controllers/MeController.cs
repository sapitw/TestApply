using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>当前会话用户相关接口（菜单、权限、个人资料）</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly IUserContext _user;
    private readonly IRolePermissionProvider _provider;

    public MeController(IUserContext user, IRolePermissionProvider provider)
    {
        _user = user;
        _provider = provider;
    }

    /// <summary>获取当前用户档案</summary>
    [HttpGet("profile")]
    public Result<object> Profile() => Result<object>.Ok(new
    {
        userId         = _user.UserId,
        userName       = _user.UserName,
        displayName    = _user.DisplayName,
        role           = _user.Role,
        departmentCode = _user.DepartmentCode,
        departmentName = _user.DepartmentName
    });

    /// <summary>获取当前角色可见的菜单</summary>
    [HttpGet("menus")]
    public Result<IReadOnlyList<MenuConfig>> Menus() =>
        Result<IReadOnlyList<MenuConfig>>.Ok(_provider.GetMenusForRole(_user.Role));

    /// <summary>获取当前角色可访问的路由白名单</summary>
    [HttpGet("routes")]
    public Result<IReadOnlySet<string>> Routes() =>
        Result<IReadOnlySet<string>>.Ok(_provider.GetAllowedRoutes(_user.Role));

    /// <summary>获取当前角色拥有的权限码集合</summary>
    [HttpGet("permissions")]
    public Result<IReadOnlySet<string>> Permissions() =>
        Result<IReadOnlySet<string>>.Ok(_provider.GetPermissionsForRole(_user.Role));
}
