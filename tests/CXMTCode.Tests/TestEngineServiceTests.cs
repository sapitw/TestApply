using CXMTCode.Modules.TestEngine;

namespace CXMTCode.Tests;

public class TestEngineServiceTests
{
    private static async Task<TestSuiteService> NewServiceAsync()
    {
        var ctx = await TestDbHelper.NewSqliteContextAsync();
        return new TestSuiteService(ctx);
    }

    [Fact]
    public async Task Create_suite_and_list()
    {
        var s = await NewServiceAsync();
        var id = await s.CreateSuiteAsync(new TestSuiteDto
        {
            SuiteName = "回归套件",
            IsGate = true,
            MinPassRate = 0.9
        }, "tester");
        id.Should().NotBeNullOrEmpty();
        var list = await s.ListSuitesAsync();
        list.Should().ContainSingle(x => x.SuiteId == id && x.IsGate);
    }

    [Fact]
    public async Task Add_cases_and_run_marks_passed()
    {
        var s = await NewServiceAsync();
        var sid = await s.CreateSuiteAsync(new TestSuiteDto { SuiteName = "S1", MinPassRate = 0.5 }, "u");
        await s.AddCaseAsync(new TestCaseDto { SuiteId = sid, CaseName = "c1", CaseType = "UNIT", Enabled = true });
        await s.AddCaseAsync(new TestCaseDto { SuiteId = sid, CaseName = "c2", CaseType = "UNIT", Enabled = true });
        await s.AddCaseAsync(new TestCaseDto { SuiteId = sid, CaseName = "c3", CaseType = "UNIT", Enabled = false });

        var run = await s.TriggerRunAsync(sid, "u");
        run.Status.Should().Be("PASSED");
        run.TotalCases.Should().Be(2);   // 仅 enabled=true 的两条
        run.PassedCases.Should().Be(2);
        run.PassRate.Should().Be(1.0);

        var runs = await s.ListRunsAsync(sid);
        runs.Should().ContainSingle();
    }

    [Fact]
    public async Task Gate_returns_true_after_passing_run()
    {
        var s = await NewServiceAsync();
        var sid = await s.CreateSuiteAsync(new TestSuiteDto { SuiteName = "G", IsGate = true, MinPassRate = 0.5 }, "u");
        await s.AddCaseAsync(new TestCaseDto { SuiteId = sid, CaseName = "c", CaseType = "UNIT", Enabled = true });
        await s.TriggerRunAsync(sid, "u");
        (await s.CheckGateAsync(sid)).Should().BeTrue();
    }

    [Fact]
    public async Task Gate_returns_true_when_not_gate()
    {
        var s = await NewServiceAsync();
        var sid = await s.CreateSuiteAsync(new TestSuiteDto { SuiteName = "X", IsGate = false }, "u");
        (await s.CheckGateAsync(sid)).Should().BeTrue();
    }
}
