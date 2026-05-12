using CXMTCode.Infrastructure.Crypto;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using CXMTCode.Modules.Schema.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>系统管理接口（SysAdmin 专属）</summary>
[ApiController]
[Authorize(Policy = "SysAdmin")]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly ISystemConfigService _config;
    private readonly IAuditLogger _audit;
    private readonly IUserContext _user;

    public AdminController(
        UserRepository users,
        ISystemConfigService config,
        IAuditLogger audit,
        IUserContext user)
    {
        _users = users; _config = config; _audit = audit; _user = user;
    }

    [HttpGet("users")]
    public async Task<Result<IReadOnlyList<UserDto>>> ListUsers()
    {
        var rows = await _users.ListAsync();
        return Result<IReadOnlyList<UserDto>>.Ok(rows.Select(u => new UserDto(u)).ToList());
    }

    [HttpPost("users")]
    public async Task<Result<UserDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        var hash = PasswordHasher.Hash(dto.Password, out var salt);
        var u = new UserRecord
        {
            UserId         = Guid.NewGuid().ToString("N"),
            UserName       = dto.UserName,
            DisplayName    = dto.DisplayName,
            Role           = dto.Role,
            DepartmentCode = dto.DepartmentCode,
            DepartmentName = dto.DepartmentName,
            Email          = dto.Email,
            PasswordHash   = hash,
            PasswordSalt   = salt,
            CreatedBy      = _user.UserId.ToString()
        };
        await _users.InsertAsync(u);
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "User.Create", $"创建用户 {dto.UserName} 角色 {dto.Role}", clientIp: _user.ClientIp);
        return Result<UserDto>.Ok(new UserDto(u));
    }

    [HttpPut("users/{id}/role")]
    public async Task<Result<bool>> UpdateRole(string id, [FromBody] UpdateRoleDto dto)
    {
        var n = await _users.UpdateRoleAsync(id, dto.Role, _user.UserId.ToString());
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "User.UpdateRole", $"调整用户 {id} 角色为 {dto.Role}", clientIp: _user.ClientIp);
        return Result<bool>.Ok(n > 0);
    }

    [HttpPut("users/{id}/active")]
    public async Task<Result<bool>> ToggleActive(string id, [FromBody] ToggleActiveDto dto)
    {
        var n = await _users.UpdateActiveAsync(id, dto.Active, _user.UserId.ToString());
        return Result<bool>.Ok(n > 0);
    }

    [HttpGet("config")]
    public async Task<Result<IReadOnlyDictionary<string, string>>> ListConfig()
        => Result<IReadOnlyDictionary<string, string>>.Ok(await _config.GetByGroupAsync("SYSTEM"));

    [HttpPut("config/{key}")]
    public async Task<Result<bool>> UpdateConfig(string key, [FromBody] ConfigValueDto dto)
    {
        await _config.SetAsync(key, dto.Value, _user.UserId.ToString());
        return Result<bool>.Ok(true);
    }
}

public class CreateUserDto
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.User;
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public string? Email { get; set; }
}

public class UpdateRoleDto { public UserRole Role { get; set; } }
public class ToggleActiveDto { public bool Active { get; set; } }
public class ConfigValueDto { public string Value { get; set; } = ""; }
