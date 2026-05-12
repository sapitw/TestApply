using CXMTCode.Infrastructure;
using CXMTCode.Kernel.Audit;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Audit;

namespace CXMTCode.Tests;

public class ComplianceReportTests
{
    private static async Task<(ComplianceReportService svc, AuditLogger logger)> NewAsync()
    {
        var ctx = await TestDbHelper.NewSqliteContextAsync();
        var logger = new AuditLogger(ctx, new SnowflakeIdGenerator(11));
        return (new ComplianceReportService(logger), logger);
    }

    [Fact]
    public async Task Build_aggregates_counts()
    {
        var (svc, audit) = await NewAsync();
        var uid = Guid.NewGuid();
        for (var i = 0; i < 8; i++)
            await audit.WriteAsync(uid, "u", UserRole.User, "Change.Submit", $"op {i}", result: "SUCCESS");
        for (var i = 0; i < 2; i++)
            await audit.WriteAsync(uid, "u", UserRole.User, "Change.Execute", $"exec {i}", result: "FAILED");

        var r = await svc.BuildAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        r.TotalLogs.Should().Be(10);
        r.CountByOperation.Should().ContainKey("Change.Submit").WhoseValue.Should().Be(8);
        r.CountByOperation.Should().ContainKey("Change.Execute").WhoseValue.Should().Be(2);
        r.CountByResult.Should().ContainKey("SUCCESS").WhoseValue.Should().Be(8);
    }

    [Fact]
    public async Task Render_html_contains_kpi_and_summary()
    {
        var (svc, audit) = await NewAsync();
        await audit.WriteAsync(Guid.NewGuid(), "u", UserRole.User, "X", "y", result: "SUCCESS");
        var r = await svc.BuildAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var html = svc.RenderHtml(r);
        html.Should().Contain("21CFR Part11");
        html.Should().Contain("总操作数");
        html.Should().Contain("LogId");
    }

    [Fact]
    public async Task Render_pdf_returns_non_empty_bytes()
    {
        var (svc, audit) = await NewAsync();
        await audit.WriteAsync(Guid.NewGuid(), "u", UserRole.User, "X", "y", result: "SUCCESS");
        var r = await svc.BuildAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var pdf = svc.RenderPdf(r);
        pdf.Length.Should().BeGreaterThan(1000);
        // PDF 文件以 %PDF- 开头
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }
}
