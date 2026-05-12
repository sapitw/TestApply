using System.Data.Common;
using Dapper;
using CXMTCode.Infrastructure.Db.SystemDb;

namespace CXMTCode.Infrastructure.Db;

/// <summary>
/// 系统 DB 仓储基类 - 所有 CXMT_* 表 CRUD 通过此类。
/// 名称沿用规格的 "OracleRepositoryBase"，但通过 <see cref="ISystemDbContext"/> 抽象同时支持 SQLite/Oracle。
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly ISystemDbContext SystemDb;
    protected OracleRepositoryBase(ISystemDbContext systemDb) => SystemDb = systemDb;

    protected DbConnection CreateConnection() => SystemDb.CreateOpenConnection();

    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<T>(sql, param);
    }

    protected async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    protected async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(sql, param);
    }

    protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<T>(sql, param);
    }
}
