using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CXMTCode.Infrastructure.Crypto;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CXMTCode.Modules.Schema.Services;

/// <summary>认证服务 - 登录、签发 JWT、刷新 Token</summary>
public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly IConfiguration _config;

    public AuthService(UserRepository users, IConfiguration config)
    {
        _users  = users;
        _config = config;
    }

    public async Task<LoginResult> LoginAsync(string userName, string password, string clientIp)
    {
        var user = await _users.GetByUserNameAsync(userName);
        if (user is null || !user.IsActive)
            return LoginResult.Failed("账号不存在或已禁用");
        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return LoginResult.Failed("用户名或密码错误");

        await _users.UpdateLastLoginAsync(user.UserId, clientIp);

        var (token, expiresIn) = IssueToken(user);
        var refresh = IssueRefreshToken(user);

        return LoginResult.Ok(new LoginPayload
        {
            Token = token,
            RefreshToken = refresh,
            ExpiresIn = expiresIn,
            User = new UserDto(user)
        });
    }

    private (string Token, int ExpiresIn) IssueToken(UserRecord u)
    {
        var jwt = _config.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwt["SecretKey"] ?? "DevSecretKeyMustBeAtLeast32CharsLong!");
        var minutes = int.TryParse(jwt["ExpirationMinutes"], out var m) ? m : 480;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.UserId),
            new Claim(ClaimTypes.Name,           u.UserName),
            new Claim(ClaimTypes.GivenName,      u.DisplayName),
            new Claim(ClaimTypes.Role,           u.Role.ToString()),
            new Claim("dept_code",               u.DepartmentCode ?? ""),
            new Claim("dept_name",               u.DepartmentName ?? "")
        };
        var token = new JwtSecurityToken(
            issuer:   jwt["Issuer"],
            audience: jwt["Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), minutes * 60);
    }

    private string IssueRefreshToken(UserRecord u) => Guid.NewGuid().ToString("N");
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public LoginPayload? Payload { get; set; }

    public static LoginResult Ok(LoginPayload payload) => new() { Success = true, Payload = payload };
    public static LoginResult Failed(string err) => new() { Success = false, ErrorMessage = err };
}

public class LoginPayload
{
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public UserDto() { }
    public UserDto(UserRecord u)
    {
        UserId         = u.UserId;
        UserName       = u.UserName;
        DisplayName    = u.DisplayName;
        Role           = u.Role;
        DepartmentCode = u.DepartmentCode;
        DepartmentName = u.DepartmentName;
        Email          = u.Email;
        IsActive       = u.IsActive;
    }
}
