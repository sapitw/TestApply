using Dapper;
using CXMTCode.Infrastructure.Crypto;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Schema.Models;

namespace CXMTCode.Modules.Schema.Repositories;

public sealed class DbConnectionRepository : OracleRepositoryBase
{
    public DbConnectionRepository(ISystemDbContext systemDb) : base(systemDb) { }

    public async Task<IReadOnlyList<DbConnectionRecord>> ListAsync(SystemEnvironment? env = null)
    {
        using var conn = CreateConnection();
        var sql = @"SELECT CONNECTION_ID, CONNECTION_NAME, DATABASE_TYPE, ENVIRONMENT_TYPE, IS_ACTIVE_ENV,
                           HOST, PORT, SERVICE_NAME, PDB_NAME, USERNAME, PASSWORD_ENC, CONNECTION_STRING_ENC,
                           STANDBY_HOST, STANDBY_PORT, STANDBY_SERVICE, IS_READ_ONLY, IS_STANDBY, IS_ACTIVE,
                           TEST_RESULT, TESTED_AT, DESCRIPTION,
                           CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY
                    FROM CXMT_DB_CONNECTIONS ";
        if (env.HasValue) sql += "WHERE ENVIRONMENT_TYPE=@env ";
        sql += "ORDER BY CREATED_AT DESC";
        var rows = await conn.QueryAsync(sql, new { env = env?.ToString() });
        return rows.Select(Map).Where(r => r != null).Cast<DbConnectionRecord>().ToList();
    }

    public async Task<DbConnectionRecord?> GetByIdAsync(string id)
    {
        using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT * FROM CXMT_DB_CONNECTIONS WHERE CONNECTION_ID=@id", new { id });
        return Map(row);
    }

    public async Task<int> InsertAsync(DbConnectionRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_DB_CONNECTIONS(
                CONNECTION_ID, CONNECTION_NAME, DATABASE_TYPE, ENVIRONMENT_TYPE, IS_ACTIVE_ENV,
                HOST, PORT, SERVICE_NAME, PDB_NAME, USERNAME, PASSWORD_ENC, CONNECTION_STRING_ENC,
                STANDBY_HOST, STANDBY_PORT, STANDBY_SERVICE, IS_READ_ONLY, IS_STANDBY, IS_ACTIVE,
                DESCRIPTION, CREATED_BY)
            VALUES(@ConnectionId, @ConnectionName, @DbType, @Env, @IsActive,
                @Host, @Port, @ServiceName, @PdbName, @Username, @PasswordEnc, @ConnectionStringEnc,
                @StandbyHost, @StandbyPort, @StandbyService, @ReadOnly, @Standby, @ActiveFlag,
                @Description, @CreatedBy)",
            new
            {
                r.ConnectionId, r.ConnectionName,
                DbType = (int)r.DatabaseType,
                Env = r.EnvironmentType.ToString(),
                IsActive = r.IsActiveEnv ? 1 : 0,
                r.Host, r.Port, r.ServiceName, r.PdbName, r.Username, r.PasswordEnc, r.ConnectionStringEnc,
                r.StandbyHost, r.StandbyPort, r.StandbyService,
                ReadOnly = r.IsReadOnly ? 1 : 0,
                Standby  = r.IsStandby  ? 1 : 0,
                ActiveFlag = r.IsActive ? 1 : 0,
                r.Description, r.CreatedBy
            });
    }

    public async Task<int> UpdateAsync(DbConnectionRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_DB_CONNECTIONS SET
                CONNECTION_NAME=@ConnectionName, DATABASE_TYPE=@DbType, ENVIRONMENT_TYPE=@Env,
                HOST=@Host, PORT=@Port, SERVICE_NAME=@ServiceName, PDB_NAME=@PdbName,
                USERNAME=@Username, PASSWORD_ENC=@PasswordEnc, CONNECTION_STRING_ENC=@ConnectionStringEnc,
                STANDBY_HOST=@StandbyHost, STANDBY_PORT=@StandbyPort, STANDBY_SERVICE=@StandbyService,
                DESCRIPTION=@Description, UPDATED_AT=CURRENT_TIMESTAMP, UPDATED_BY=@UpdatedBy
            WHERE CONNECTION_ID=@ConnectionId",
            new
            {
                r.ConnectionId, r.ConnectionName,
                DbType = (int)r.DatabaseType,
                Env = r.EnvironmentType.ToString(),
                r.Host, r.Port, r.ServiceName, r.PdbName, r.Username, r.PasswordEnc, r.ConnectionStringEnc,
                r.StandbyHost, r.StandbyPort, r.StandbyService,
                r.Description, r.UpdatedBy
            });
    }

    public async Task<int> DeleteAsync(string id)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM CXMT_DB_CONNECTIONS WHERE CONNECTION_ID=@id", new { id });
    }

    public async Task<int> SetActiveEnvAsync(string id)
    {
        using var conn = CreateConnection();
        var record = await GetByIdAsync(id);
        if (record is null) return 0;
        // 把同类型同环境其它连接的 IS_ACTIVE_ENV 清零
        await conn.ExecuteAsync(@"
            UPDATE CXMT_DB_CONNECTIONS SET IS_ACTIVE_ENV=0
            WHERE DATABASE_TYPE=@t AND ENVIRONMENT_TYPE=@e AND CONNECTION_ID<>@id",
            new { t = (int)record.DatabaseType, e = record.EnvironmentType.ToString(), id });
        return await conn.ExecuteAsync(
            "UPDATE CXMT_DB_CONNECTIONS SET IS_ACTIVE_ENV=1 WHERE CONNECTION_ID=@id", new { id });
    }

    public async Task RecordTestAsync(string id, bool success, string? version, string? error)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE CXMT_DB_CONNECTIONS SET TEST_RESULT=@r, TESTED_AT=CURRENT_TIMESTAMP WHERE CONNECTION_ID=@id",
            new { r = success ? $"OK:{version}" : $"FAIL:{error}", id });
    }

    private static DbConnectionRecord? Map(dynamic? r)
    {
        if (r is null) return null;
        return new DbConnectionRecord
        {
            ConnectionId        = (string)r.CONNECTION_ID,
            ConnectionName      = (string)r.CONNECTION_NAME,
            DatabaseType        = (DatabaseType)Convert.ToInt32(r.DATABASE_TYPE),
            EnvironmentType     = Enum.TryParse<SystemEnvironment>((string)r.ENVIRONMENT_TYPE, true, out SystemEnvironment env) ? env : SystemEnvironment.PROD,
            IsActiveEnv         = Convert.ToInt32(r.IS_ACTIVE_ENV) == 1,
            Host                = (string)r.HOST,
            Port                = Convert.ToInt32(r.PORT),
            ServiceName         = (string?)r.SERVICE_NAME,
            PdbName             = (string?)r.PDB_NAME,
            Username            = (string)r.USERNAME,
            PasswordEnc         = (string?)r.PASSWORD_ENC ?? "",
            ConnectionStringEnc = (string?)r.CONNECTION_STRING_ENC,
            StandbyHost         = (string?)r.STANDBY_HOST,
            StandbyPort         = r.STANDBY_PORT is null ? null : (int?)Convert.ToInt32(r.STANDBY_PORT),
            StandbyService     = (string?)r.STANDBY_SERVICE,
            IsReadOnly          = Convert.ToInt32(r.IS_READ_ONLY) == 1,
            IsStandby           = Convert.ToInt32(r.IS_STANDBY) == 1,
            IsActive            = Convert.ToInt32(r.IS_ACTIVE) == 1,
            TestResult          = (string?)r.TEST_RESULT,
            TestedAt            = TryParse((string?)r.TESTED_AT),
            Description         = (string?)r.DESCRIPTION,
            CreatedAt           = TryParse((string?)r.CREATED_AT) ?? DateTime.UtcNow,
            CreatedBy           = (string?)r.CREATED_BY,
            UpdatedAt           = TryParse((string?)r.UPDATED_AT),
            UpdatedBy           = (string?)r.UPDATED_BY
        };
    }

    private static DateTime? TryParse(string? s) => DateTime.TryParse(s, out var dt) ? dt : (DateTime?)null;

    /// <summary>SM4 加密 DB 连接密码</summary>
    public static string EncryptPassword(string plain) => CryptoHelper.EncryptSm4(plain);

    /// <summary>SM4 解密 DB 连接密码</summary>
    public static string DecryptPassword(string cipher) => CryptoHelper.DecryptSm4(cipher);
}
