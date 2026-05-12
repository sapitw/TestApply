using Dapper;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Interfaces;

namespace CXMTCode.Kernel.Configuration;

/// <summary>
/// 配置管理中枢 - 读写 CXMT_SYSTEM_CONFIG。
/// 简单的 KV 实现，未做缓存（系统配置访问量低）。
/// </summary>
public sealed class SystemConfigService : ISystemConfigService
{
    private readonly ISystemDbContext _db;
    public SystemConfigService(ISystemDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key)
    {
        using var conn = _db.CreateOpenConnection();
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT CONFIG_VALUE FROM CXMT_SYSTEM_CONFIG WHERE CONFIG_KEY=@k", new { k = key });
    }

    public async Task<T?> GetAsync<T>(string key) where T : class, new()
    {
        var value = await GetAsync(key);
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<T>(value); }
        catch { return null; }
    }

    public async Task SetAsync(string key, string value, string? updatedBy = null)
    {
        using var conn = _db.CreateOpenConnection();
        var exists = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM CXMT_SYSTEM_CONFIG WHERE CONFIG_KEY=@k", new { k = key });
        if (exists == 0)
        {
            await conn.ExecuteAsync(
                "INSERT INTO CXMT_SYSTEM_CONFIG(CONFIG_KEY, CONFIG_VALUE, UPDATED_BY) VALUES(@k, @v, @by)",
                new { k = key, v = value, by = updatedBy });
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE CXMT_SYSTEM_CONFIG SET CONFIG_VALUE=@v, UPDATED_AT=CURRENT_TIMESTAMP, UPDATED_BY=@by WHERE CONFIG_KEY=@k",
                new { k = key, v = value, by = updatedBy });
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        using var conn = _db.CreateOpenConnection();
        var c = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM CXMT_SYSTEM_CONFIG WHERE CONFIG_KEY=@k", new { k = key });
        return c > 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetByGroupAsync(string group)
    {
        using var conn = _db.CreateOpenConnection();
        var rows = await conn.QueryAsync<(string Key, string Value)>(
            "SELECT CONFIG_KEY AS Key, CONFIG_VALUE AS Value FROM CXMT_SYSTEM_CONFIG WHERE CONFIG_GROUP=@g",
            new { g = group });
        return rows.ToDictionary(r => r.Key, r => r.Value);
    }
}
