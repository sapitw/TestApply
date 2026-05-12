using Dapper;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;

namespace CXMTCode.Modules.TestEngine;

/// <summary>
/// 测试套件 / 用例 / 执行管理 - F1-F5 测试引擎的持久化层 + 编排服务。
/// </summary>
public sealed class TestSuiteService : OracleRepositoryBase
{
    public TestSuiteService(ISystemDbContext db) : base(db) { }

    public async Task<IReadOnlyList<TestSuiteDto>> ListSuitesAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TestSuiteDto>(@"
            SELECT SUITE_ID    AS SuiteId,
                   SUITE_NAME  AS SuiteName,
                   DESCRIPTION AS Description,
                   IS_GATE     AS IsGate,
                   MIN_PASS_RATE AS MinPassRate,
                   CREATED_BY  AS CreatedBy,
                   CREATED_AT  AS CreatedAt
            FROM CXMT_TEST_SUITES ORDER BY CREATED_AT DESC");
        return rows.ToList();
    }

    public async Task<string> CreateSuiteAsync(TestSuiteDto dto, string createdBy)
    {
        var id = Guid.NewGuid().ToString("N");
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO CXMT_TEST_SUITES(SUITE_ID, SUITE_NAME, DESCRIPTION, IS_GATE, MIN_PASS_RATE, CREATED_BY)
            VALUES(@id, @SuiteName, @Description, @IsGate, @MinPassRate, @CreatedBy)",
            new
            {
                id,
                dto.SuiteName,
                dto.Description,
                IsGate = dto.IsGate ? 1 : 0,
                dto.MinPassRate,
                CreatedBy = createdBy
            });
        return id;
    }

    public async Task<IReadOnlyList<TestCaseDto>> ListCasesAsync(string suiteId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TestCaseDto>(@"
            SELECT CASE_ID    AS CaseId,
                   SUITE_ID   AS SuiteId,
                   CASE_NAME  AS CaseName,
                   CASE_TYPE  AS CaseType,
                   TARGET_SQL AS TargetSql,
                   EXPECTED   AS Expected,
                   ORDINAL    AS Ordinal,
                   ENABLED    AS Enabled
            FROM CXMT_TEST_CASES WHERE SUITE_ID=@sid ORDER BY ORDINAL, CASE_NAME", new { sid = suiteId });
        return rows.ToList();
    }

    public async Task<string> AddCaseAsync(TestCaseDto dto)
    {
        var id = Guid.NewGuid().ToString("N");
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO CXMT_TEST_CASES(CASE_ID, SUITE_ID, CASE_NAME, CASE_TYPE, TARGET_SQL, EXPECTED, ORDINAL, ENABLED)
            VALUES(@id, @SuiteId, @CaseName, @CaseType, @TargetSql, @Expected, @Ordinal, @Enabled)",
            new
            {
                id,
                dto.SuiteId, dto.CaseName, dto.CaseType, dto.TargetSql, dto.Expected,
                dto.Ordinal,
                Enabled = dto.Enabled ? 1 : 0
            });
        return id;
    }

    /// <summary>F1+F5 触发一次测试运行（占位执行：标记为通过）。生产环境应调度真实 Runner</summary>
    public async Task<TestRunDto> TriggerRunAsync(string suiteId, string triggeredBy)
    {
        var runId = Guid.NewGuid().ToString("N");
        using var conn = CreateConnection();
        var cases = await conn.QueryAsync<TestCaseDto>(
            "SELECT CASE_ID AS CaseId, ENABLED AS Enabled FROM CXMT_TEST_CASES WHERE SUITE_ID=@sid", new { sid = suiteId });
        var enabled = cases.Count(c => c.Enabled);

        // 简化执行：直接判定为全部通过（接入真实 Runner 后替换此逻辑）
        var passed = enabled;
        var failed = 0;
        var rate   = enabled == 0 ? 1.0 : (double)passed / enabled;

        await conn.ExecuteAsync(@"
            INSERT INTO CXMT_TEST_RUNS(RUN_ID, SUITE_ID, STATUS, FINISHED_AT,
                TOTAL_CASES, PASSED_CASES, FAILED_CASES, PASS_RATE, TRIGGERED_BY)
            VALUES(@id, @sid, @status, CURRENT_TIMESTAMP, @t, @p, @f, @r, @by)",
            new
            {
                id = runId, sid = suiteId,
                status = failed == 0 ? "PASSED" : "FAILED",
                t = enabled, p = passed, f = failed, r = rate, by = triggeredBy
            });

        return new TestRunDto
        {
            RunId = runId, SuiteId = suiteId,
            Status = failed == 0 ? "PASSED" : "FAILED",
            TotalCases = enabled, PassedCases = passed, FailedCases = failed,
            PassRate = rate, TriggeredBy = triggeredBy,
            StartedAt = DateTime.UtcNow, FinishedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<TestRunDto>> ListRunsAsync(string? suiteId = null, int take = 50)
    {
        using var conn = CreateConnection();
        var sql = @"
            SELECT RUN_ID       AS RunId,
                   SUITE_ID     AS SuiteId,
                   STATUS       AS Status,
                   STARTED_AT   AS StartedAt,
                   FINISHED_AT  AS FinishedAt,
                   TOTAL_CASES  AS TotalCases,
                   PASSED_CASES AS PassedCases,
                   FAILED_CASES AS FailedCases,
                   PASS_RATE    AS PassRate,
                   TRIGGERED_BY AS TriggeredBy
            FROM CXMT_TEST_RUNS";
        if (!string.IsNullOrEmpty(suiteId)) sql += " WHERE SUITE_ID=@sid";
        sql += " ORDER BY STARTED_AT DESC LIMIT @take";
        var rows = await conn.QueryAsync<TestRunDto>(sql, new { sid = suiteId, take });
        return rows.ToList();
    }

    /// <summary>F2 测试门禁 - 检查指定套件最近一次执行是否达标</summary>
    public async Task<bool> CheckGateAsync(string suiteId)
    {
        using var conn = CreateConnection();
        var suite = await conn.QueryFirstOrDefaultAsync<(int IsGate, double MinPassRate)>(
            "SELECT IS_GATE AS IsGate, MIN_PASS_RATE AS MinPassRate FROM CXMT_TEST_SUITES WHERE SUITE_ID=@sid",
            new { sid = suiteId });
        if (suite.IsGate != 1) return true;
        var lastRun = await conn.QueryFirstOrDefaultAsync<double?>(@"
            SELECT PASS_RATE FROM CXMT_TEST_RUNS WHERE SUITE_ID=@sid ORDER BY STARTED_AT DESC LIMIT 1",
            new { sid = suiteId });
        return lastRun.HasValue && lastRun.Value >= suite.MinPassRate;
    }
}

public class TestSuiteDto
{
    public string SuiteId { get; set; } = "";
    public string SuiteName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsGate { get; set; }
    public double MinPassRate { get; set; } = 0.95;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TestCaseDto
{
    public string CaseId { get; set; } = "";
    public string SuiteId { get; set; } = "";
    public string CaseName { get; set; } = "";
    public string CaseType { get; set; } = "UNIT";
    public string? TargetSql { get; set; }
    public string? Expected { get; set; }
    public int Ordinal { get; set; }
    public bool Enabled { get; set; } = true;
}

public class TestRunDto
{
    public string RunId { get; set; } = "";
    public string SuiteId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int TotalCases { get; set; }
    public int PassedCases { get; set; }
    public int FailedCases { get; set; }
    public double PassRate { get; set; }
    public string? TriggeredBy { get; set; }
}
