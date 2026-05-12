using Dapper;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Schema.Models;

namespace CXMTCode.Modules.Schema.Repositories;

public sealed class ChangeRequestRepository : OracleRepositoryBase
{
    public ChangeRequestRepository(ISystemDbContext systemDb) : base(systemDb) { }

    public async Task<IReadOnlyList<ChangeRequestRecord>> ListAsync(string? applicantId = null, int pageIndex = 0, int pageSize = 50)
    {
        using var conn = CreateConnection();
        var sql = "SELECT * FROM CXMT_CHANGE_REQUESTS WHERE 1=1 ";
        if (!string.IsNullOrEmpty(applicantId)) sql += " AND APPLICANT_ID=@aid ";
        sql += " ORDER BY CREATED_AT DESC LIMIT @take OFFSET @skip";
        var rows = await conn.QueryAsync(sql, new { aid = applicantId, take = pageSize, skip = pageIndex * pageSize });
        return rows.Select(Map).Cast<ChangeRequestRecord>().ToList();
    }

    public async Task<ChangeRequestRecord?> GetAsync(string requestId)
    {
        using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT * FROM CXMT_CHANGE_REQUESTS WHERE REQUEST_ID=@id", new { id = requestId });
        return row is null ? null : Map(row);
    }

    public async Task<int> InsertAsync(ChangeRequestRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_CHANGE_REQUESTS(REQUEST_ID, STATUS, OPERATION_TYPE, DATABASE_TYPE,
                CONNECTION_ID, TARGET_TABLE, SQL_STATEMENT, SQL_HASH, REASON, IMPACT_LEVEL,
                APPLICANT_ID, APPLICANT_ROLE, CREATED_AT)
            VALUES(@RequestId, @Status, @OpType, @DbType, @ConnectionId, @TargetTable,
                @SqlStatement, @SqlHash, @Reason, @ImpactLevel, @ApplicantId, @ApplicantRole, CURRENT_TIMESTAMP)",
            new
            {
                r.RequestId,
                Status = (int)r.Status,
                OpType = (int)r.OperationType,
                DbType = (int)r.DatabaseType,
                r.ConnectionId, r.TargetTable, r.SqlStatement, r.SqlHash, r.Reason, r.ImpactLevel,
                r.ApplicantId,
                ApplicantRole = (int)r.ApplicantRole
            });
    }

    public async Task<int> UpdateStatusAsync(string requestId, ChangeRequestStatus status, string? approverId = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_CHANGE_REQUESTS SET STATUS=@s,
                   APPROVER_ID = COALESCE(@aid, APPROVER_ID),
                   APPROVED_AT = CASE WHEN @s=3 THEN CURRENT_TIMESTAMP ELSE APPROVED_AT END
            WHERE REQUEST_ID=@id",
            new { s = (int)status, aid = approverId, id = requestId });
    }

    public async Task<int> SetDryRunResultAsync(string requestId, string dryRunJson, long affectedRows, string? backupTable, string? rollbackSql)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_CHANGE_REQUESTS SET DRY_RUN_RESULT=@dr, AFFECTED_ROWS=@ar,
                BACKUP_TABLE=@bt, ROLLBACK_SQL=@rs
            WHERE REQUEST_ID=@id",
            new { dr = dryRunJson, ar = affectedRows, bt = backupTable, rs = rollbackSql, id = requestId });
    }

    public async Task<int> SetExecutedAsync(string requestId, bool success, string? execResultJson, string executedBy)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_CHANGE_REQUESTS SET STATUS=@s, EXECUTED_AT=CURRENT_TIMESTAMP,
                EXECUTED_BY=@by, EXEC_RESULT=@er, COMPLETED_AT=CURRENT_TIMESTAMP
            WHERE REQUEST_ID=@id",
            new
            {
                s = success ? (int)ChangeRequestStatus.Executed : (int)ChangeRequestStatus.Failed,
                by = executedBy,
                er = execResultJson,
                id = requestId
            });
    }

    public async Task<int> SetRolledBackAsync(string requestId, string rolledBackBy)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_CHANGE_REQUESTS SET STATUS=@s, ROLLED_BACK_AT=CURRENT_TIMESTAMP, ROLLED_BACK_BY=@by
            WHERE REQUEST_ID=@id",
            new { s = (int)ChangeRequestStatus.RolledBack, by = rolledBackBy, id = requestId });
    }

    private static ChangeRequestRecord Map(dynamic r) => new()
    {
        RequestId      = (string)r.REQUEST_ID,
        Status         = (ChangeRequestStatus)Convert.ToInt32(r.STATUS),
        OperationType  = (SqlOperationType)Convert.ToInt32(r.OPERATION_TYPE),
        DatabaseType   = (DatabaseType)Convert.ToInt32(r.DATABASE_TYPE),
        ConnectionId   = (string)r.CONNECTION_ID,
        TargetTable    = (string)r.TARGET_TABLE,
        SqlStatement   = (string)r.SQL_STATEMENT,
        SqlHash        = (string?)r.SQL_HASH,
        Reason         = (string?)r.REASON ?? "",
        ImpactLevel    = Convert.ToInt32(r.IMPACT_LEVEL),
        AffectedRows   = r.AFFECTED_ROWS is null ? null : (long?)Convert.ToInt64(r.AFFECTED_ROWS),
        ApplicantId    = (string)r.APPLICANT_ID,
        ApplicantRole  = (UserRole)Convert.ToInt32(r.APPLICANT_ROLE),
        ApproverId     = (string?)r.APPROVER_ID,
        ApprovedAt     = ParseDate((string?)r.APPROVED_AT),
        DryRunResult   = (string?)r.DRY_RUN_RESULT,
        BackupTable    = (string?)r.BACKUP_TABLE,
        RollbackSql    = (string?)r.ROLLBACK_SQL,
        ExecutedAt     = ParseDate((string?)r.EXECUTED_AT),
        ExecutedBy     = (string?)r.EXECUTED_BY,
        ExecResult     = (string?)r.EXEC_RESULT,
        RolledBackAt   = ParseDate((string?)r.ROLLED_BACK_AT),
        RolledBackBy   = (string?)r.ROLLED_BACK_BY,
        CreatedAt      = ParseDate((string?)r.CREATED_AT) ?? DateTime.UtcNow,
        CompletedAt    = ParseDate((string?)r.COMPLETED_AT)
    };

    private static DateTime? ParseDate(string? s) => DateTime.TryParse(s, out var dt) ? dt : (DateTime?)null;
}
