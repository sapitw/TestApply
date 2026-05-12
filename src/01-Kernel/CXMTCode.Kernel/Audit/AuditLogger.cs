using Dapper;
using CXMTCode.Infrastructure;
using CXMTCode.Infrastructure.Crypto;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Audit;

/// <summary>
/// 审计日志中枢 - Append Only 写入，SM3 哈希链防篡改。
/// 每条日志的 HASH_VALUE = SM3(prevHash + 业务字段)，可通过 <see cref="VerifyIntegrityAsync"/> 验证完整性。
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly ISystemDbContext _db;
    private readonly SnowflakeIdGenerator _idGen;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AuditLogger(ISystemDbContext db, SnowflakeIdGenerator? idGen = null)
    {
        _db    = db;
        _idGen = idGen ?? new SnowflakeIdGenerator();
    }

    public async Task<AuditLogWriteResult> WriteAsync(
        Guid userId, string userName, UserRole userRole,
        string operationType, string operationContent,
        string? targetDb = null, string? targetTable = null,
        string? sqlStatement = null, string? beforeData = null, string? afterData = null,
        string? clientIp = null, string? result = null,
        SystemEnvironment? environment = null)
    {
        await _writeLock.WaitAsync();
        try
        {
            using var conn = _db.CreateOpenConnection();
            var prevHash = await conn.ExecuteScalarAsync<string?>(
                "SELECT HASH_VALUE FROM CXMT_AUDIT_LOGS ORDER BY LOG_ID DESC LIMIT 1");

            var logId = _idGen.NextId();
            var now   = DateTime.UtcNow;
            var payload = $"{logId}|{now:O}|{userId}|{userName}|{(int)userRole}|{operationType}|{operationContent}|{targetDb}|{targetTable}|{sqlStatement}|{prevHash ?? string.Empty}";
            var hash = CryptoHelper.ComputeSm3Hash(payload);

            await conn.ExecuteAsync(@"
                INSERT INTO CXMT_AUDIT_LOGS(
                    LOG_ID, LOG_TIME, USER_ID, USER_NAME, USER_ROLE, ENVIRONMENT_TYPE,
                    OPERATION_TYPE, OPERATION_DESC, TARGET_DATABASE, TARGET_TABLE,
                    SQL_STATEMENT, BEFORE_DATA, AFTER_DATA, CLIENT_IP, RESULT,
                    HASH_VALUE, PREV_HASH)
                VALUES(@LogId, @LogTime, @UserId, @UserName, @Role, @Env,
                       @OpType, @OpDesc, @TargetDb, @TargetTable,
                       @Sql, @Before, @After, @Ip, @Result,
                       @Hash, @PrevHash)",
                new
                {
                    LogId = logId,
                    LogTime = now.ToString("O"),
                    UserId = userId.ToString(),
                    UserName = userName,
                    Role = (int)userRole,
                    Env = environment?.ToString(),
                    OpType = operationType,
                    OpDesc = operationContent,
                    TargetDb = targetDb,
                    TargetTable = targetTable,
                    Sql = sqlStatement,
                    Before = beforeData,
                    After = afterData,
                    Ip = clientIp,
                    Result = result ?? "SUCCESS",
                    Hash = hash,
                    PrevHash = prevHash
                });

            return new AuditLogWriteResult { Success = true, LogId = logId, HashCode = hash };
        }
        catch (Exception ex)
        {
            return new AuditLogWriteResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> VerifyIntegrityAsync(long logId)
    {
        using var conn = _db.CreateOpenConnection();
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM CXMT_AUDIT_LOGS WHERE LOG_ID=@id", new { id = logId });
        if (row is null) return false;

        string userIdStr = (string)row.USER_ID;
        var payload = $"{row.LOG_ID}|{row.LOG_TIME}|{userIdStr}|{row.USER_NAME}|{row.USER_ROLE}|{row.OPERATION_TYPE}|{row.OPERATION_DESC}|{row.TARGET_DATABASE}|{row.TARGET_TABLE}|{row.SQL_STATEMENT}|{row.PREV_HASH ?? string.Empty}";
        var recomputed = CryptoHelper.ComputeSm3Hash(payload);
        return string.Equals(recomputed, (string)row.HASH_VALUE, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        Guid? userId = null, DateTime? from = null, DateTime? to = null,
        string? operationType = null, int pageIndex = 0, int pageSize = 50)
    {
        var sql = @"SELECT LOG_ID, LOG_TIME, USER_ID, USER_NAME, USER_ROLE, ENVIRONMENT_TYPE,
                           OPERATION_TYPE, OPERATION_DESC, TARGET_DATABASE, TARGET_TABLE,
                           SQL_STATEMENT, BEFORE_DATA, AFTER_DATA, CLIENT_IP, RESULT,
                           HASH_VALUE, PREV_HASH
                    FROM CXMT_AUDIT_LOGS WHERE 1=1 ";
        var parameters = new DynamicParameters();
        if (userId.HasValue)              { sql += " AND USER_ID = @uid";   parameters.Add("uid", userId.Value.ToString()); }
        if (!string.IsNullOrEmpty(operationType)) { sql += " AND OPERATION_TYPE = @op"; parameters.Add("op", operationType); }
        if (from.HasValue) { sql += " AND LOG_TIME >= @from"; parameters.Add("from", from.Value.ToString("O")); }
        if (to.HasValue)   { sql += " AND LOG_TIME <= @to";   parameters.Add("to",   to.Value.ToString("O")); }
        sql += " ORDER BY LOG_ID DESC LIMIT @take OFFSET @skip";
        parameters.Add("take", pageSize);
        parameters.Add("skip", pageIndex * pageSize);

        using var conn = _db.CreateOpenConnection();
        var rows = await conn.QueryAsync(sql, parameters);
        return rows.Select(r => new AuditLogEntry
        {
            LogId           = Convert.ToInt64(r.LOG_ID),
            LogTime         = DateTime.TryParse((string?)r.LOG_TIME, out DateTime dt) ? dt : DateTime.MinValue,
            UserId          = Guid.TryParse((string?)r.USER_ID, out Guid uid) ? uid : Guid.Empty,
            UserName        = (string?)r.USER_NAME ?? string.Empty,
            UserRole        = (UserRole)Convert.ToInt32(r.USER_ROLE),
            EnvironmentType = Enum.TryParse<SystemEnvironment>((string?)r.ENVIRONMENT_TYPE, true, out SystemEnvironment env) ? env : (SystemEnvironment?)null,
            OperationType   = (string?)r.OPERATION_TYPE ?? string.Empty,
            OperationDesc   = (string?)r.OPERATION_DESC ?? string.Empty,
            TargetDatabase  = (string?)r.TARGET_DATABASE,
            TargetTable     = (string?)r.TARGET_TABLE,
            SqlStatement    = (string?)r.SQL_STATEMENT,
            BeforeData      = (string?)r.BEFORE_DATA,
            AfterData       = (string?)r.AFTER_DATA,
            ClientIp        = (string?)r.CLIENT_IP,
            Result          = (string?)r.RESULT ?? "SUCCESS",
            HashValue       = (string?)r.HASH_VALUE ?? string.Empty,
            PrevHash        = (string?)r.PREV_HASH
        }).ToList();
    }
}
