using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Services;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>认证相关接口 - 登录 / 当前用户信息</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    /// <summary>用户登录</summary>
    [HttpPost("login")]
    public async Task<Result<LoginPayload>> Login([FromBody] LoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var r  = await _auth.LoginAsync(req.UserName, req.Password, ip);
        return r.Success && r.Payload is not null
            ? Result<LoginPayload>.Ok(r.Payload)
            : Result<LoginPayload>.Fail(r.ErrorMessage ?? "登录失败", "LOGIN_FAILED");
    }
}

public class LoginRequest
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}
