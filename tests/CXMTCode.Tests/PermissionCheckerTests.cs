using CXMTCode.Kernel.Audit;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Security;
using CXMTCode.Infrastructure;
using CXMTCode.Infrastructure.Db.SystemDb;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace CXMTCode.Tests;

public class PermissionCheckerTests
{
    private static (PermissionChecker checker, IUserContext user) Build(UserRole role = UserRole.User)
    {
        var ctx = TestDbHelper.NewSqliteContextAsync().GetAwaiter().GetResult();
        var audit = new AuditLogger(ctx, new SnowflakeIdGenerator(2));
        var u = TestUserContext.Of(role);
        return (new PermissionChecker(audit), u);
    }

    [Fact]
    public async Task RL001_Drop_should_be_blocked()
    {
        var (c, u) = Build(UserRole.SysAdmin);
        var r = await c.CheckSqlExecutionPermissionAsync(u, "DROP TABLE FOO", DatabaseType.Oracle19c);
        r.IsAllowed.Should().BeFalse();
        r.RuleCode.Should().Be("RL001");
    }

    [Fact]
    public async Task RL003_Update_without_where_should_be_blocked()
    {
        var (c, u) = Build();
        var r = await c.CheckSqlExecutionPermissionAsync(u, "UPDATE T SET A=1", DatabaseType.Oracle19c);
        r.IsAllowed.Should().BeFalse();
        r.RuleCode.Should().Be("RL003");
    }

    [Fact]
    public async Task RL004_Delete_without_where_should_be_blocked()
    {
        var (c, u) = Build();
        var r = await c.CheckSqlExecutionPermissionAsync(u, "DELETE FROM T", DatabaseType.Oracle19c);
        r.IsAllowed.Should().BeFalse();
        r.RuleCode.Should().Be("RL004");
    }

    [Fact]
    public async Task Safe_select_should_pass()
    {
        var (c, u) = Build();
        var r = await c.CheckSqlExecutionPermissionAsync(u, "SELECT * FROM T WHERE ID=1", DatabaseType.Oracle19c);
        r.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Role_hierarchy_works()
    {
        var (c, _) = Build();
        var u = TestUserContext.Of(UserRole.SysAdmin);
        var r = await c.CheckRoleAsync(u, UserRole.DBA);
        r.IsAllowed.Should().BeTrue();

        var lowU = TestUserContext.Of(UserRole.User);
        var r2 = await c.CheckRoleAsync(lowU, UserRole.DBA);
        r2.IsAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("TRUNCATE TABLE FOO", "RL002")]
    [InlineData("GRANT SELECT ON FOO TO PUBLIC", "RL006")]
    [InlineData("CREATE USER X IDENTIFIED BY X", "RL007")]
    [InlineData("ALTER SYSTEM SET FOO=BAR", "RL008")]
    [InlineData("SELECT * FROM T UNION SELECT * FROM B WHERE 1=1", "RL010")]
    public async Task Red_lines_block_high_risk(string sql, string expectedCode)
    {
        var (c, u) = Build();
        var r = await c.CheckSqlExecutionPermissionAsync(u, sql, DatabaseType.Oracle19c);
        r.IsAllowed.Should().BeFalse();
        r.RuleCode.Should().Be(expectedCode);
    }
}

internal sealed class TestUserContext : IUserContext
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = "u";
    public string DisplayName { get; init; } = "U";
    public UserRole Role { get; init; }
    public string DepartmentCode { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string ClientIp { get; init; } = "127.0.0.1";
    public bool IsAuthenticated { get; init; } = true;
    public bool IsDbaOrAbove => (int)Role >= (int)UserRole.DBA;
    public bool IsSysAdmin => Role == UserRole.SysAdmin;
    public bool HasRole(UserRole r) => (int)Role >= (int)r;
    public static TestUserContext Of(UserRole r) => new() { Role = r, UserId = Guid.NewGuid() };
}
