using Dapper;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;

namespace CXMTCode.Modules.Schema.Services;

/// <summary>
/// 环境切换服务（SysAdmin 专属）。
/// 在 CXMT_SYSTEM_CONFIG.CURRENT_ENVIRONMENT 写入新值，同时记录 CXMT_ENV_SWITCH_LOG。
/// </summary>
public sealed class EnvironmentService
{
    private readonly ISystemDbContext _db;
    private readonly ISystemConfigService _config;
    private readonly IAuditLogger _audit;

    public EnvironmentService(ISystemDbContext db, ISystemConfigService config, IAuditLogger audit)
    {
        _db = db; _config = config; _audit = audit;
    }

    public async Task<SystemEnvironment> GetCurrentAsync()
    {
        var v = await _config.GetAsync("CURRENT_ENVIRONMENT");
        return Enum.TryParse<SystemEnvironment>(v, true, out var env) ? env : SystemEnvironment.PROD;
    }

    public async Task<bool> SwitchAsync(SystemEnvironment target, string? reason, IUserContext user)
    {
        if (!user.IsSysAdmin) return false;

        var current = await GetCurrentAsync();
        if (current == target) return true;

        await _config.SetAsync("CURRENT_ENVIRONMENT", target.ToString(), user.UserName);

        using var conn = _db.CreateOpenConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO CXMT_ENV_SWITCH_LOG(LOG_ID, FROM_ENV, TO_ENV, SWITCHED_BY, SWITCHED_BY_ROLE, CLIENT_IP, REASON)
            VALUES(@id, @from, @to, @uid, @role, @ip, @r)",
            new
            {
                id = Guid.NewGuid().ToString("N"),
                from = current.ToString(),
                to = target.ToString(),
                uid = user.UserId.ToString(),
                role = (int)user.Role,
                ip = user.ClientIp,
                r = reason
            });

        await _audit.WriteAsync(user.UserId, user.UserName, user.Role,
            "System.EnvSwitch",
            $"环境切换 {current} → {target}",
            result: "SUCCESS", clientIp: user.ClientIp, environment: target);
        return true;
    }
}
