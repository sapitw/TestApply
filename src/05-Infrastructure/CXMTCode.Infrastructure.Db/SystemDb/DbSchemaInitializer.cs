using Dapper;
using CXMTCode.Infrastructure.Crypto;

namespace CXMTCode.Infrastructure.Db.SystemDb;

/// <summary>
/// 系统 DB 启动期初始化 - 在 SQLite 上等价创建 CXMT_* 表（仅开发用）。
/// 生产 Oracle 用 <c>db/oracle-init.sql</c> 由 DBA 执行。
/// </summary>
public static class DbSchemaInitializer
{
    public static async Task EnsureCreatedAsync(ISystemDbContext ctx)
    {
        if (!ctx.Dialect.IsSqlite) return; // Oracle 通过 DDL 脚本初始化

        using var conn = ctx.CreateOpenConnection();
        foreach (var ddl in SqliteDdl)
        {
            await conn.ExecuteAsync(ddl);
        }

        // 默认管理员：admin / admin@123 （SM3 占位 = SHA256("salt:admin@123")）
        var exists = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM CXMT_USERS WHERE USER_NAME='admin'");
        if (exists == 0)
        {
            var hash = PasswordHasher.Hash("admin@123", out var salt);
            await conn.ExecuteAsync(@"
                INSERT INTO CXMT_USERS(USER_ID, USER_NAME, PASSWORD_HASH, PASSWORD_SALT,
                    DISPLAY_NAME, ROLE, IS_ACTIVE, CREATED_AT)
                VALUES(@id, 'admin', @h, @s, '系统管理员', 3, 1, CURRENT_TIMESTAMP)",
                new { id = Guid.NewGuid().ToString("N"), h = hash, s = salt });
        }

        // 默认系统配置
        await conn.ExecuteAsync(@"
            INSERT OR IGNORE INTO CXMT_SYSTEM_CONFIG(CONFIG_KEY, CONFIG_VALUE, DESCRIPTION, CONFIG_GROUP)
            VALUES('CURRENT_ENVIRONMENT','PROD','当前系统激活环境','SYSTEM'),
                  ('ALLOW_ENV_SWITCH','1','是否允许SysAdmin切换环境','SYSTEM')");
    }

    private static readonly string[] SqliteDdl = new[]
    {
        @"CREATE TABLE IF NOT EXISTS CXMT_USERS(
            USER_ID         TEXT PRIMARY KEY,
            USER_NAME       TEXT NOT NULL UNIQUE,
            PASSWORD_HASH   TEXT NOT NULL,
            PASSWORD_SALT   TEXT NOT NULL,
            DISPLAY_NAME    TEXT NOT NULL,
            ROLE            INTEGER NOT NULL,
            DEPARTMENT_CODE TEXT,
            DEPARTMENT_NAME TEXT,
            EMAIL           TEXT,
            IS_ACTIVE       INTEGER DEFAULT 1,
            LAST_LOGIN_AT   TEXT,
            LAST_LOGIN_IP   TEXT,
            CREATED_AT      TEXT DEFAULT CURRENT_TIMESTAMP,
            CREATED_BY      TEXT,
            UPDATED_AT      TEXT,
            UPDATED_BY      TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_DB_CONNECTIONS(
            CONNECTION_ID         TEXT PRIMARY KEY,
            CONNECTION_NAME       TEXT NOT NULL UNIQUE,
            DATABASE_TYPE         INTEGER NOT NULL,
            ENVIRONMENT_TYPE      TEXT NOT NULL,
            IS_ACTIVE_ENV         INTEGER DEFAULT 0,
            HOST                  TEXT NOT NULL,
            PORT                  INTEGER NOT NULL,
            SERVICE_NAME          TEXT,
            PDB_NAME              TEXT,
            USERNAME              TEXT NOT NULL,
            PASSWORD_ENC          TEXT NOT NULL,
            CONNECTION_STRING_ENC TEXT,
            STANDBY_HOST          TEXT,
            STANDBY_PORT          INTEGER,
            STANDBY_SERVICE       TEXT,
            IS_READ_ONLY          INTEGER DEFAULT 0,
            IS_STANDBY            INTEGER DEFAULT 0,
            IS_ACTIVE             INTEGER DEFAULT 1,
            TEST_RESULT           TEXT,
            TESTED_AT             TEXT,
            DESCRIPTION           TEXT,
            CREATED_AT            TEXT DEFAULT CURRENT_TIMESTAMP,
            CREATED_BY            TEXT,
            UPDATED_AT            TEXT,
            UPDATED_BY            TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_SYSTEM_CONFIG(
            CONFIG_KEY   TEXT PRIMARY KEY,
            CONFIG_VALUE TEXT NOT NULL,
            DESCRIPTION  TEXT,
            CONFIG_GROUP TEXT DEFAULT 'SYSTEM',
            IS_EDITABLE  INTEGER DEFAULT 1,
            CREATED_AT   TEXT DEFAULT CURRENT_TIMESTAMP,
            UPDATED_AT   TEXT,
            UPDATED_BY   TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_AUDIT_LOGS(
            LOG_ID           INTEGER PRIMARY KEY,
            LOG_TIME         TEXT NOT NULL,
            USER_ID          TEXT NOT NULL,
            USER_NAME        TEXT NOT NULL,
            USER_ROLE        INTEGER NOT NULL,
            ENVIRONMENT_TYPE TEXT,
            OPERATION_TYPE   TEXT NOT NULL,
            OPERATION_DESC   TEXT NOT NULL,
            TARGET_DATABASE  TEXT,
            TARGET_TABLE     TEXT,
            SQL_STATEMENT    TEXT,
            BEFORE_DATA      TEXT,
            AFTER_DATA       TEXT,
            CLIENT_IP        TEXT,
            RESULT           TEXT,
            HASH_VALUE       TEXT NOT NULL,
            PREV_HASH        TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_CHANGE_REQUESTS(
            REQUEST_ID     TEXT PRIMARY KEY,
            STATUS         INTEGER NOT NULL,
            OPERATION_TYPE INTEGER NOT NULL,
            DATABASE_TYPE  INTEGER NOT NULL,
            CONNECTION_ID  TEXT NOT NULL,
            TARGET_TABLE   TEXT NOT NULL,
            SQL_STATEMENT  TEXT NOT NULL,
            SQL_HASH       TEXT,
            REASON         TEXT NOT NULL,
            IMPACT_LEVEL   INTEGER NOT NULL,
            AFFECTED_ROWS  INTEGER,
            APPLICANT_ID   TEXT NOT NULL,
            APPLICANT_ROLE INTEGER NOT NULL,
            APPROVER_ID    TEXT,
            APPROVED_AT    TEXT,
            DRY_RUN_RESULT TEXT,
            BACKUP_TABLE   TEXT,
            ROLLBACK_SQL   TEXT,
            EXECUTED_AT    TEXT,
            EXECUTED_BY    TEXT,
            EXEC_RESULT    TEXT,
            ROLLED_BACK_AT TEXT,
            ROLLED_BACK_BY TEXT,
            CREATED_AT     TEXT DEFAULT CURRENT_TIMESTAMP,
            COMPLETED_AT   TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_TABLE_ACCESS(
            ACCESS_ID       TEXT PRIMARY KEY,
            CONNECTION_ID   TEXT NOT NULL,
            TABLE_NAME      TEXT NOT NULL,
            TABLE_SCHEMA    TEXT,
            STATUS          INTEGER DEFAULT 0,
            DBA_APPROVER    TEXT,
            BIZ_APPROVER    TEXT,
            HAS_PRIMARY_KEY INTEGER,
            HAS_UNIQUE_IDX  INTEGER,
            RISK_LEVEL      INTEGER,
            CREATED_AT      TEXT DEFAULT CURRENT_TIMESTAMP,
            APPROVED_AT     TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_TABLE_PERMISSIONS(
            PERMISSION_ID TEXT PRIMARY KEY,
            ACCESS_ID     TEXT NOT NULL,
            USER_ID       TEXT NOT NULL,
            ALLOW_SELECT  INTEGER DEFAULT 0,
            ALLOW_INSERT  INTEGER DEFAULT 0,
            ALLOW_UPDATE  INTEGER DEFAULT 0,
            ALLOW_DELETE  INTEGER DEFAULT 0,
            GRANTED_BY    TEXT,
            GRANTED_AT    TEXT DEFAULT CURRENT_TIMESTAMP,
            STATUS        INTEGER DEFAULT 1
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_DELETE_TEMPLATES(
            TEMPLATE_ID       TEXT PRIMARY KEY,
            TEMPLATE_NAME     TEXT NOT NULL,
            CONNECTION_ID     TEXT NOT NULL,
            TABLE_NAME        TEXT NOT NULL,
            TEMPLATE_SQL      TEXT NOT NULL,
            PARAMETERS_JSON   TEXT,
            MAX_AFFECTED_ROWS INTEGER DEFAULT 1000,
            STATUS            INTEGER DEFAULT 0,
            VERSION           INTEGER DEFAULT 1,
            CREATED_BY        TEXT,
            CREATED_AT        TEXT DEFAULT CURRENT_TIMESTAMP,
            APPROVED_BY       TEXT,
            APPROVED_AT       TEXT,
            FIRST_EXECUTED_AT TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_WHITELIST_RULES(
            RULE_ID         TEXT PRIMARY KEY,
            RULE_NAME       TEXT NOT NULL,
            TABLE_NAME      TEXT NOT NULL,
            DATABASE_TYPE   INTEGER NOT NULL,
            ALLOWED_COLUMNS TEXT,
            MAX_AFFECTED    INTEGER DEFAULT 1000,
            TIME_WINDOW     TEXT,
            STATUS          INTEGER DEFAULT 1,
            CREATED_BY      TEXT,
            CREATED_AT      TEXT DEFAULT CURRENT_TIMESTAMP
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_APPROVAL_INSTANCES(
            INSTANCE_ID  TEXT PRIMARY KEY,
            REQUEST_TYPE TEXT NOT NULL,
            REQUEST_ID   TEXT NOT NULL,
            STATUS       INTEGER DEFAULT 0,
            CURRENT_NODE INTEGER DEFAULT 0,
            NODES_JSON   TEXT NOT NULL,
            CREATED_AT   TEXT DEFAULT CURRENT_TIMESTAMP,
            COMPLETED_AT TEXT
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_APPROVAL_RECORDS(
            RECORD_ID     TEXT PRIMARY KEY,
            INSTANCE_ID   TEXT NOT NULL,
            NODE_ID       TEXT NOT NULL,
            APPROVER_ID   TEXT NOT NULL,
            APPROVER_ROLE INTEGER,
            ACTION        TEXT NOT NULL,
            COMMENT_TEXT  TEXT,
            SIGNATURE     TEXT,
            CREATED_AT    TEXT DEFAULT CURRENT_TIMESTAMP
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_ENV_SWITCH_LOG(
            LOG_ID           TEXT PRIMARY KEY,
            FROM_ENV         TEXT NOT NULL,
            TO_ENV           TEXT NOT NULL,
            SWITCHED_BY      TEXT NOT NULL,
            SWITCHED_BY_ROLE INTEGER NOT NULL,
            CLIENT_IP        TEXT,
            REASON           TEXT,
            CREATED_AT       TEXT DEFAULT CURRENT_TIMESTAMP
        )",
        @"CREATE TABLE IF NOT EXISTS CXMT_SESSION_ENV_OVERRIDE(
            SESSION_ID   TEXT PRIMARY KEY,
            USER_ID      TEXT NOT NULL,
            OVERRIDE_ENV TEXT NOT NULL,
            CREATED_AT   TEXT DEFAULT CURRENT_TIMESTAMP,
            EXPIRES_AT   TEXT NOT NULL
        )"
    };
}
