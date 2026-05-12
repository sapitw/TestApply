using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Security;
using FluentAssertions;

namespace CXMTCode.Tests;

public class RolePermissionTests
{
    private readonly RolePermissionProvider _p = new();

    [Fact]
    public void User_can_submit_change()
    {
        _p.HasPermission(UserRole.User, "change:create:submit").Should().BeTrue();
    }

    [Fact]
    public void User_cannot_switch_env()
    {
        _p.HasPermission(UserRole.User, "env:switch").Should().BeFalse();
        _p.HasPermission(UserRole.DBA,  "env:switch").Should().BeFalse();
        _p.HasPermission(UserRole.SysAdmin, "env:switch").Should().BeTrue();
    }

    [Fact]
    public void DBA_inherits_user_permissions()
    {
        _p.HasPermission(UserRole.DBA, "change:create:submit").Should().BeTrue();
    }

    [Fact]
    public void Routes_grow_with_role()
    {
        var u = _p.GetAllowedRoutes(UserRole.User).Count;
        var d = _p.GetAllowedRoutes(UserRole.DBA).Count;
        var s = _p.GetAllowedRoutes(UserRole.SysAdmin).Count;
        u.Should().BeLessThan(d);
        d.Should().BeLessThan(s);
    }
}
