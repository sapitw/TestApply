using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>环境管理接口 - 查询当前环境（所有用户），切换环境（仅 SysAdmin）</summary>
[ApiController]
[Authorize]
public sealed class EnvironmentController : ControllerBase
{
    private readonly EnvironmentService _env;
    private readonly IUserContext _user;

    public EnvironmentController(EnvironmentService env, IUserContext user)
    {
        _env  = env;
        _user = user;
    }

    [HttpGet("api/environment/current")]
    public async Task<Result<SystemEnvironment>> Current()
        => Result<SystemEnvironment>.Ok(await _env.GetCurrentAsync());

    [HttpPost("api/admin/environment/switch")]
    [Authorize(Policy = "SysAdmin")]
    public async Task<Result<SystemEnvironment>> Switch([FromBody] SwitchEnvRequest req)
    {
        if (!_user.IsSysAdmin)
            return Result<SystemEnvironment>.Fail("仅 SysAdmin 可切换环境", "FORBIDDEN");
        var ok = await _env.SwitchAsync(req.TargetEnvironment, req.Reason, _user);
        return ok
            ? Result<SystemEnvironment>.Ok(req.TargetEnvironment)
            : Result<SystemEnvironment>.Fail("切换失败");
    }
}

public class SwitchEnvRequest
{
    public SystemEnvironment TargetEnvironment { get; set; } = SystemEnvironment.PROD;
    public string? Reason { get; set; }
}
