using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Security;

/// <summary>
/// 角色权限提供者 - 硬编码权限码、菜单、路由白名单。
/// </summary>
public sealed class RolePermissionProvider : IRolePermissionProvider
{
    private static readonly Dictionary<UserRole, HashSet<string>> RolePerms = new()
    {
        [UserRole.User] = new(StringComparer.OrdinalIgnoreCase)
        {
            "change:view:list", "change:create:submit", "change:view:detail",
            "change:edit:draft", "change:delete:draft", "change:execute:dryrun",
            "change:execute:actual", "change:execute:rollback", "change:view:preview",
            "audit:view:own", "audit:export:own",
            "user:view:profile", "user:edit:profile"
        },
        [UserRole.DBA] = new(StringComparer.OrdinalIgnoreCase)
        {
            "tableaccess:view:list", "tableaccess:create:submit", "tableaccess:approve:dba",
            "tableaccess:edit:config",
            "template:view:list", "template:create:submit", "template:edit:modify",
            "template:delete:disable", "template:approve:activate",
            "rule:view:list", "rule:create:submit", "rule:edit:modify", "rule:delete:disable",
            "dbconfig:view:list", "dbconfig:create:add", "dbconfig:edit:modify",
            "usergrant:view:list", "usergrant:create:grant", "usergrant:edit:modify",
            "audit:view:all", "audit:export:all"
        },
        [UserRole.SysAdmin] = new(StringComparer.OrdinalIgnoreCase)
        {
            "usermgmt:view:list", "usermgmt:create:add", "usermgmt:edit:modify",
            "usermgmt:delete:disable", "usermgmt:role:assign", "usermgmt:role:revoke",
            "env:switch",
            "test:view:dashboard", "test:create:suite", "test:execute:run",
            "test:execute:parallel", "test:view:report", "test:config:threshold",
            "test:manage:environment",
            "report:view:all", "report:export:pdf", "report:export:html",
            "sysconfig:view:list", "sysconfig:edit:modify", "sysconfig:view:logs",
            "audit:view:system"
        }
    };

    public IReadOnlySet<string> GetPermissionsForRole(UserRole role) =>
        RolePerms.TryGetValue(role, out var p) ? p : new HashSet<string>();

    public bool HasPermission(UserRole role, string code)
    {
        for (var r = (int)role; r >= (int)UserRole.User; r--)
            if (RolePerms.TryGetValue((UserRole)r, out var perms) && perms.Contains(code))
                return true;
        return false;
    }

    private static readonly Dictionary<UserRole, List<MenuConfig>> RoleMenus = BuildMenus();

    private static Dictionary<UserRole, List<MenuConfig>> BuildMenus()
    {
        var userMenus = new List<MenuConfig>
        {
            new() { Key = "dashboard", Label = "工作台", Path = "/dashboard", Icon = "DashboardOutlined" },
            new()
            {
                Key = "change", Label = "变更管理", Path = "/change", Icon = "EditOutlined",
                Children = new()
                {
                    new() { Key = "change-apply",    Label = "变更申请", Path = "/change/apply" },
                    new() { Key = "change-list",     Label = "我的申请", Path = "/change/list" },
                    new() { Key = "change-rollback", Label = "回滚操作", Path = "/change/rollback" }
                }
            },
            new()
            {
                Key = "audit", Label = "审计查询", Path = "/audit", Icon = "FileSearchOutlined",
                Children = new()
                {
                    new() { Key = "audit-own", Label = "我的审计记录", Path = "/audit/own" }
                }
            }
        };

        var dbaMenus = CloneAndUpdate(userMenus, audit =>
        {
            audit.Children = new() { new() { Key = "audit-all", Label = "全部审计记录", Path = "/audit/all" } };
        });
        dbaMenus.Add(new()
        {
            Key = "dba", Label = "DBA 管理", Path = "/dba", Icon = "DatabaseOutlined", RequiredRole = UserRole.DBA,
            Children = new()
            {
                new() { Key = "dba-tableaccess", Label = "表准入管理",  Path = "/dba/table-access" },
                new() { Key = "dba-template",    Label = "DELETE 模板", Path = "/dba/templates" },
                new() { Key = "dba-rule",        Label = "白名单规则",  Path = "/dba/rules" },
                new() { Key = "dba-dbconn",      Label = "DB 连接配置", Path = "/dba/db-connections" },
                new() { Key = "dba-usergrant",   Label = "用户授权",    Path = "/dba/user-grants" }
            }
        });

        var sysAdminMenus = new List<MenuConfig>(dbaMenus)
        {
            new()
            {
                Key = "admin", Label = "系统管理", Path = "/admin", Icon = "SettingOutlined", RequiredRole = UserRole.SysAdmin,
                Children = new()
                {
                    new() { Key = "admin-users",   Label = "用户与角色", Path = "/admin/users" },
                    new() { Key = "admin-test",    Label = "测试管理",   Path = "/admin/test-mgmt" },
                    new() { Key = "admin-reports", Label = "自检报告",   Path = "/admin/reports" },
                    new() { Key = "admin-config",  Label = "系统配置",   Path = "/admin/config" }
                }
            }
        };

        return new Dictionary<UserRole, List<MenuConfig>>
        {
            [UserRole.User]     = userMenus,
            [UserRole.DBA]      = dbaMenus,
            [UserRole.SysAdmin] = sysAdminMenus
        };
    }

    private static List<MenuConfig> CloneAndUpdate(List<MenuConfig> source, Action<MenuConfig> mutateAudit)
    {
        var clone = source.Select(m => new MenuConfig
        {
            Key = m.Key, Label = m.Label, Path = m.Path, Icon = m.Icon, RequiredRole = m.RequiredRole,
            Children = m.Children?.Select(c => new MenuConfig
            {
                Key = c.Key, Label = c.Label, Path = c.Path, Icon = c.Icon, RequiredRole = c.RequiredRole,
                Children = c.Children
            }).ToList()
        }).ToList();
        var auditNode = clone.FirstOrDefault(m => m.Key == "audit");
        if (auditNode is not null) mutateAudit(auditNode);
        return clone;
    }

    public IReadOnlyList<MenuConfig> GetMenusForRole(UserRole role) =>
        RoleMenus.TryGetValue(role, out var m) ? m : new List<MenuConfig>();

    public IReadOnlySet<string> GetAllowedRoutes(UserRole role)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/", "/dashboard", "/login", "/profile", "/feature-summary", "/forbidden"
        };
        Collect(GetMenusForRole(role), routes);
        return routes;
    }

    private static void Collect(IEnumerable<MenuConfig> menus, HashSet<string> routes)
    {
        foreach (var m in menus)
        {
            if (!string.IsNullOrEmpty(m.Path)) routes.Add(m.Path);
            if (m.Children is not null) Collect(m.Children, routes);
        }
    }
}
