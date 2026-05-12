using System.Text;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CXMTCode.Modules.Audit;

/// <summary>
/// 21CFR Part11 合规报告生成器。
/// 输出两种格式：HTML（在线查看 / 打印）、PDF（QuestPDF，离线归档）。
/// 内容包含：报告期范围、审计统计、抽样验证哈希链、操作明细。
/// </summary>
public sealed class ComplianceReportService
{
    private readonly IAuditLogger _audit;

    static ComplianceReportService()
    {
        // QuestPDF 社区许可证（运行时一次性设置）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ComplianceReportService(IAuditLogger audit) => _audit = audit;

    public async Task<ComplianceReport> BuildAsync(DateTime from, DateTime to)
    {
        var logs = await _audit.QueryAsync(null, from, to, null, 0, 10_000);

        var byOp = logs
            .GroupBy(l => l.OperationType)
            .ToDictionary(g => g.Key, g => g.Count());
        var byResult = logs
            .GroupBy(l => l.Result)
            .ToDictionary(g => g.Key, g => g.Count());

        // 抽样验证哈希链：每 50 条抽 1 条
        var sample = logs.Where((_, i) => i % 50 == 0).Take(20).ToList();
        var verified = new List<long>();
        var failed   = new List<long>();
        foreach (var s in sample)
        {
            if (await _audit.VerifyIntegrityAsync(s.LogId)) verified.Add(s.LogId);
            else failed.Add(s.LogId);
        }

        return new ComplianceReport
        {
            From = from,
            To = to,
            TotalLogs = logs.Count,
            CountByOperation = byOp,
            CountByResult    = byResult,
            SampleSize       = sample.Count,
            VerifiedSamples  = verified,
            FailedSamples    = failed,
            Logs             = logs.Take(500).ToList()  // 报告里展示前 500 条
        };
    }

    public string RenderHtml(ComplianceReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>CXMTCode 21CFR Part11 合规自检报告</title>");
        sb.AppendLine(@"<style>
            body{font-family:'Noto Sans SC','Microsoft YaHei',sans-serif;padding:24px;color:#111;background:#fff}
            h1{color:#0055cc;border-bottom:2px solid #0088ff;padding-bottom:8px}
            h2{color:#0055cc;margin-top:24px}
            table{border-collapse:collapse;width:100%;margin:8px 0;font-size:12px}
            th,td{border:1px solid #ccc;padding:6px 10px;text-align:left}
            th{background:#e6f0ff}
            .kpi{display:inline-block;margin-right:16px;padding:12px 16px;border:1px solid #0088ff;border-radius:6px;background:#f3f8ff}
            .kpi .v{font-size:28px;color:#0055cc;font-weight:bold;display:block}
            .pass{color:#16a34a}.fail{color:#dc2626}
        </style></head><body>");
        sb.AppendLine("<h1>CXMTCode 21CFR Part11 合规自检报告</h1>");
        sb.Append($"<p>报告期：<b>{r.From:yyyy-MM-dd HH:mm}</b> ~ <b>{r.To:yyyy-MM-dd HH:mm}</b></p>");
        sb.Append($"<div class='kpi'><span class='v'>{r.TotalLogs}</span>总操作数</div>");
        sb.Append($"<div class='kpi'><span class='v'>{r.SampleSize}</span>抽样数</div>");
        sb.Append($"<div class='kpi'><span class='v pass'>{r.VerifiedSamples.Count}</span>哈希一致</div>");
        sb.Append($"<div class='kpi'><span class='v fail'>{r.FailedSamples.Count}</span>哈希异常</div>");

        sb.AppendLine("<h2>按操作类型统计</h2><table><tr><th>操作类型</th><th>次数</th></tr>");
        foreach (var kv in r.CountByOperation.OrderByDescending(k => k.Value))
            sb.Append($"<tr><td>{kv.Key}</td><td>{kv.Value}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>按执行结果统计</h2><table><tr><th>结果</th><th>次数</th></tr>");
        foreach (var kv in r.CountByResult.OrderByDescending(k => k.Value))
            sb.Append($"<tr><td>{kv.Key}</td><td>{kv.Value}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>哈希链抽样验证结果</h2>");
        sb.Append($"<p class='pass'>通过：{string.Join(", ", r.VerifiedSamples)}</p>");
        if (r.FailedSamples.Any())
            sb.Append($"<p class='fail'>异常：{string.Join(", ", r.FailedSamples)}</p>");

        sb.AppendLine("<h2>操作明细（最多 500 条）</h2><table>");
        sb.Append("<tr><th>LogId</th><th>时间</th><th>用户</th><th>操作</th><th>描述</th><th>结果</th></tr>");
        foreach (var l in r.Logs)
            sb.Append($"<tr><td>{l.LogId}</td><td>{l.LogTime:yyyy-MM-dd HH:mm:ss}</td><td>{l.UserName}</td><td>{l.OperationType}</td><td>{Escape(l.OperationDesc)}</td><td>{l.Result}</td></tr>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    public byte[] RenderPdf(ComplianceReport r)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("CXMTCode 21CFR Part11 合规自检报告").FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"报告期：{r.From:yyyy-MM-dd HH:mm} ~ {r.To:yyyy-MM-dd HH:mm}").FontSize(10);
                    col.Item().PaddingBottom(8).LineHorizontal(1).LineColor(Colors.Blue.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // KPI 表
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text($"{r.TotalLogs}").FontSize(20).Bold();
                            c.Item().Text("总操作数").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text($"{r.SampleSize}").FontSize(20).Bold();
                            c.Item().Text("抽样数").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text($"{r.VerifiedSamples.Count}").FontSize(20).Bold();
                            c.Item().Text("哈希一致").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Red.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text($"{r.FailedSamples.Count}").FontSize(20).Bold();
                            c.Item().Text("哈希异常").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(8).Text("按操作类型统计").Bold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Blue.Lighten3).Padding(4).Text("操作类型").Bold();
                            h.Cell().Background(Colors.Blue.Lighten3).Padding(4).Text("次数").Bold();
                        });
                        foreach (var kv in r.CountByOperation.OrderByDescending(k => k.Value))
                        {
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(kv.Key);
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{kv.Value}");
                        }
                    });

                    col.Item().PaddingTop(8).Text("操作明细 (取前 200 条)").Bold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);
                            c.ConstantColumn(110);
                            c.ConstantColumn(70);
                            c.ConstantColumn(80);
                            c.RelativeColumn();
                            c.ConstantColumn(50);
                        });
                        t.Header(h =>
                        {
                            foreach (var col0 in new[] { "LogId", "时间", "用户", "操作", "描述", "结果" })
                                h.Cell().Background(Colors.Blue.Lighten3).Padding(3).Text(col0).Bold().FontSize(9);
                        });
                        foreach (var l in r.Logs.Take(200))
                        {
                            t.Cell().Padding(3).Text($"{l.LogId}").FontSize(8);
                            t.Cell().Padding(3).Text($"{l.LogTime:MM-dd HH:mm:ss}").FontSize(8);
                            t.Cell().Padding(3).Text(l.UserName).FontSize(8);
                            t.Cell().Padding(3).Text(l.OperationType).FontSize(8);
                            t.Cell().Padding(3).Text(l.OperationDesc).FontSize(8);
                            t.Cell().Padding(3).Text(l.Result).FontSize(8);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("CXMTCode V3.0 · 合规报告 · 生成时间 ");
                    t.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}").Bold();
                });
            });
        });
        return doc.GeneratePdf();
    }

    private static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
}

public class ComplianceReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalLogs { get; set; }
    public Dictionary<string, int> CountByOperation { get; set; } = new();
    public Dictionary<string, int> CountByResult { get; set; } = new();
    public int SampleSize { get; set; }
    public List<long> VerifiedSamples { get; set; } = new();
    public List<long> FailedSamples { get; set; } = new();
    public List<AuditLogEntry> Logs { get; set; } = new();
}
