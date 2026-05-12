using Dapper;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Schema.Models;

namespace CXMTCode.Modules.Schema.Repositories;

/// <summary>表准入仓储</summary>
public sealed class TableAccessRepository : OracleRepositoryBase
{
    public TableAccessRepository(ISystemDbContext db) : base(db) { }

    public async Task<IReadOnlyList<TableAccessRecord>> ListAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT ACCESS_ID, CONNECTION_ID, TABLE_NAME, TABLE_SCHEMA, STATUS,
                   DBA_APPROVER, BIZ_APPROVER, HAS_PRIMARY_KEY, HAS_UNIQUE_IDX, RISK_LEVEL,
                   CREATED_AT, APPROVED_AT
            FROM CXMT_TABLE_ACCESS ORDER BY CREATED_AT DESC");
        return rows.Select(Map).ToList();
    }

    public async Task<int> InsertAsync(TableAccessRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_TABLE_ACCESS(ACCESS_ID, CONNECTION_ID, TABLE_NAME, TABLE_SCHEMA, STATUS,
                                          DBA_APPROVER, BIZ_APPROVER, HAS_PRIMARY_KEY, HAS_UNIQUE_IDX, RISK_LEVEL)
            VALUES(@AccessId, @ConnectionId, @TableName, @TableSchema, @Status,
                   @DbaApprover, @BizApprover, @HasPk, @HasUk, @Risk)",
            new
            {
                r.AccessId, r.ConnectionId, r.TableName, r.TableSchema, r.Status,
                r.DbaApprover, r.BizApprover,
                HasPk = r.HasPrimaryKey ? 1 : 0,
                HasUk = r.HasUniqueIdx ? 1 : 0,
                Risk = (int)r.RiskLevel
            });
    }

    public async Task<int> ApproveAsync(string accessId, string approver)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_TABLE_ACCESS SET STATUS=1, APPROVED_AT=CURRENT_TIMESTAMP, DBA_APPROVER=@by
            WHERE ACCESS_ID=@id", new { id = accessId, by = approver });
    }

    private static TableAccessRecord Map(dynamic r) => new()
    {
        AccessId       = (string)r.ACCESS_ID,
        ConnectionId   = (string)r.CONNECTION_ID,
        TableName      = (string)r.TABLE_NAME,
        TableSchema    = (string?)r.TABLE_SCHEMA,
        Status         = Convert.ToInt32(r.STATUS),
        DbaApprover    = (string?)r.DBA_APPROVER,
        BizApprover    = (string?)r.BIZ_APPROVER,
        HasPrimaryKey  = r.HAS_PRIMARY_KEY is null ? false : Convert.ToInt32(r.HAS_PRIMARY_KEY) == 1,
        HasUniqueIdx   = r.HAS_UNIQUE_IDX is null ? false : Convert.ToInt32(r.HAS_UNIQUE_IDX) == 1,
        RiskLevel      = r.RISK_LEVEL is null ? RiskLevel.Low : (RiskLevel)Convert.ToInt32(r.RISK_LEVEL),
        CreatedAt      = DateTime.TryParse((string?)r.CREATED_AT, out DateTime d1) ? d1 : DateTime.UtcNow,
        ApprovedAt     = DateTime.TryParse((string?)r.APPROVED_AT, out DateTime d2) ? d2 : (DateTime?)null
    };
}

/// <summary>DELETE 模板仓储</summary>
public sealed class DeleteTemplateRepository : OracleRepositoryBase
{
    public DeleteTemplateRepository(ISystemDbContext db) : base(db) { }

    public async Task<IReadOnlyList<DeleteTemplateRecord>> ListAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT TEMPLATE_ID, TEMPLATE_NAME, CONNECTION_ID, TABLE_NAME, TEMPLATE_SQL,
                   PARAMETERS_JSON, MAX_AFFECTED_ROWS, STATUS, VERSION,
                   CREATED_BY, CREATED_AT, APPROVED_BY, APPROVED_AT, FIRST_EXECUTED_AT
            FROM CXMT_DELETE_TEMPLATES ORDER BY CREATED_AT DESC");
        return rows.Select(Map).ToList();
    }

    public async Task<int> InsertAsync(DeleteTemplateRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_DELETE_TEMPLATES(TEMPLATE_ID, TEMPLATE_NAME, CONNECTION_ID, TABLE_NAME, TEMPLATE_SQL,
                                              PARAMETERS_JSON, MAX_AFFECTED_ROWS, STATUS, VERSION, CREATED_BY)
            VALUES(@TemplateId, @TemplateName, @ConnectionId, @TableName, @TemplateSql,
                   @ParametersJson, @MaxAffectedRows, @Status, @Version, @CreatedBy)",
            new { r.TemplateId, r.TemplateName, r.ConnectionId, r.TableName, r.TemplateSql,
                  r.ParametersJson, r.MaxAffectedRows, r.Status, r.Version, r.CreatedBy });
    }

    public async Task<int> ApproveAsync(string id, string approver)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_DELETE_TEMPLATES SET STATUS=2, APPROVED_BY=@by, APPROVED_AT=CURRENT_TIMESTAMP
            WHERE TEMPLATE_ID=@id", new { id, by = approver });
    }

    private static DeleteTemplateRecord Map(dynamic r) => new()
    {
        TemplateId       = (string)r.TEMPLATE_ID,
        TemplateName     = (string)r.TEMPLATE_NAME,
        ConnectionId     = (string)r.CONNECTION_ID,
        TableName        = (string)r.TABLE_NAME,
        TemplateSql      = (string)r.TEMPLATE_SQL,
        ParametersJson   = (string?)r.PARAMETERS_JSON,
        MaxAffectedRows  = Convert.ToInt32(r.MAX_AFFECTED_ROWS),
        Status           = Convert.ToInt32(r.STATUS),
        Version          = Convert.ToInt32(r.VERSION),
        CreatedBy        = (string?)r.CREATED_BY,
        CreatedAt        = DateTime.TryParse((string?)r.CREATED_AT, out DateTime d1) ? d1 : DateTime.UtcNow,
        ApprovedBy       = (string?)r.APPROVED_BY,
        ApprovedAt       = DateTime.TryParse((string?)r.APPROVED_AT, out DateTime d2) ? d2 : (DateTime?)null,
        FirstExecutedAt  = DateTime.TryParse((string?)r.FIRST_EXECUTED_AT, out DateTime d3) ? d3 : (DateTime?)null
    };
}

/// <summary>白名单规则仓储</summary>
public sealed class WhitelistRuleRepository : OracleRepositoryBase
{
    public WhitelistRuleRepository(ISystemDbContext db) : base(db) { }

    public async Task<IReadOnlyList<WhitelistRuleRecord>> ListAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT RULE_ID, RULE_NAME, TABLE_NAME, DATABASE_TYPE, ALLOWED_COLUMNS,
                   MAX_AFFECTED, TIME_WINDOW, STATUS, CREATED_BY, CREATED_AT
            FROM CXMT_WHITELIST_RULES ORDER BY CREATED_AT DESC");
        return rows.Select(Map).ToList();
    }

    public async Task<int> InsertAsync(WhitelistRuleRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_WHITELIST_RULES(RULE_ID, RULE_NAME, TABLE_NAME, DATABASE_TYPE, ALLOWED_COLUMNS,
                                             MAX_AFFECTED, TIME_WINDOW, STATUS, CREATED_BY)
            VALUES(@RuleId, @RuleName, @TableName, @DbType, @AllowedColumns,
                   @MaxAffected, @TimeWindow, @Status, @CreatedBy)",
            new { r.RuleId, r.RuleName, r.TableName, DbType = (int)r.DatabaseType,
                  r.AllowedColumns, r.MaxAffected, r.TimeWindow, r.Status, r.CreatedBy });
    }

    public async Task<int> DeleteAsync(string id)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM CXMT_WHITELIST_RULES WHERE RULE_ID=@id", new { id });
    }

    private static WhitelistRuleRecord Map(dynamic r) => new()
    {
        RuleId         = (string)r.RULE_ID,
        RuleName       = (string)r.RULE_NAME,
        TableName      = (string)r.TABLE_NAME,
        DatabaseType   = (DatabaseType)Convert.ToInt32(r.DATABASE_TYPE),
        AllowedColumns = (string?)r.ALLOWED_COLUMNS,
        MaxAffected    = Convert.ToInt32(r.MAX_AFFECTED),
        TimeWindow     = (string?)r.TIME_WINDOW,
        Status         = Convert.ToInt32(r.STATUS),
        CreatedBy      = (string?)r.CREATED_BY,
        CreatedAt      = DateTime.TryParse((string?)r.CREATED_AT, out DateTime d) ? d : DateTime.UtcNow
    };
}
