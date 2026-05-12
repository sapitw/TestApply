using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CXMTCode.Tests;

public class ApiIntegrationTests : IClassFixture<ApiAppFactory>
{
    private readonly HttpClient _client;
    public ApiIntegrationTests(ApiAppFactory f) => _client = f.CreateClient();

    [Fact]
    public async Task Root_should_return_info()
    {
        var resp = await _client.GetAsync("/");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("CXMTCode");
    }

    [Fact]
    public async Task Login_with_default_admin_should_succeed_and_return_jwt()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { userName = "admin", password = "admin@123" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginEnvelope>();
        body!.success.Should().BeTrue();
        body.data!.token.Should().NotBeNullOrEmpty();
        body.data.user.role.Should().Be("SysAdmin");
    }

    [Fact]
    public async Task Login_with_wrong_password_should_fail()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { userName = "admin", password = "wrong" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginEnvelope>();
        body!.success.Should().BeFalse();
        body.errorCode.Should().Be("LOGIN_FAILED");
    }

    [Fact]
    public async Task Menus_should_be_role_aware()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { userName = "admin", password = "admin@123" });
        var loginBody = await login.Content.ReadFromJsonAsync<LoginEnvelope>();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody!.data!.token);

        var resp = await _client.GetAsync("/api/me/menus");
        resp.EnsureSuccessStatusCode();
        var menus = await resp.Content.ReadAsStringAsync();
        menus.Should().Contain("系统管理");
        menus.Should().Contain("DBA 管理");
    }

    [Fact]
    public async Task Current_environment_should_default_to_prod()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { userName = "admin", password = "admin@123" });
        var loginBody = await login.Content.ReadFromJsonAsync<LoginEnvelope>();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody!.data!.token);

        var resp = await _client.GetAsync("/api/environment/current");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("PROD");
    }

    public class LoginEnvelope
    {
        public bool success { get; set; }
        public string? errorMessage { get; set; }
        public string? errorCode { get; set; }
        public LoginData? data { get; set; }
    }
    public class LoginData
    {
        public string token { get; set; } = "";
        public LoginUser user { get; set; } = new();
    }
    public class LoginUser
    {
        public string userId { get; set; } = "";
        public string role { get; set; } = "";
    }
}

public sealed class ApiAppFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // 用临时 SQLite 文件，避免与开发库冲突
        var tmp = Path.Combine(Path.GetTempPath(), $"cxmtcode-test-{Guid.NewGuid():N}.db");
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SystemDb"] = $"Data Source={tmp}",
        }));
        return base.CreateHost(builder);
    }
}
