using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>审计查询接口</summary>
[ApiController]
[Authorize]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditLogger _audit;
    private readonly IUserContext _user;

    public AuditController(IAuditLogger audit, IUserContext user)
    {
        _audit = audit; _user = user;
    }

    [HttpGet("own")]
    public async Task<Result<IReadOnlyList<AuditLogEntry>>> Own(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? operationType,
        [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 50)
        => Result<IReadOnlyList<AuditLogEntry>>.Ok(
            await _audit.QueryAsync(_user.UserId, from, to, operationType, pageIndex, pageSize));

    [HttpGet("all")]
    [Authorize(Policy = "DBA")]
    public async Task<Result<IReadOnlyList<AuditLogEntry>>> All(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? operationType,
        [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 50)
        => Result<IReadOnlyList<AuditLogEntry>>.Ok(
            await _audit.QueryAsync(userId, from, to, operationType, pageIndex, pageSize));

    [HttpGet("verify/{logId:long}")]
    [Authorize(Policy = "SysAdmin")]
    public async Task<Result<bool>> Verify(long logId)
        => Result<bool>.Ok(await _audit.VerifyIntegrityAsync(logId));

    /// <summary>21CFR Part11 合规报告 - JSON 摘要</summary>
    [HttpGet("compliance-report")]
    [Authorize(Policy = "SysAdmin")]
    public async Task<Result<ComplianceReport>> ComplianceReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] ComplianceReportService service)
    {
        var report = await service.BuildAsync(from, to);
        return Result<ComplianceReport>.Ok(report);
    }

    /// <summary>21CFR Part11 合规报告 - HTML 下载</summary>
    [HttpGet("compliance-report/html")]
    [Authorize(Policy = "SysAdmin")]
    public async Task<IActionResult> ComplianceReportHtml(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] ComplianceReportService service)
    {
        var report = await service.BuildAsync(from, to);
        var html = service.RenderHtml(report);
        return File(System.Text.Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8",
            $"compliance-{from:yyyyMMdd}-{to:yyyyMMdd}.html");
    }

    /// <summary>21CFR Part11 合规报告 - PDF 下载</summary>
    [HttpGet("compliance-report/pdf")]
    [Authorize(Policy = "SysAdmin")]
    public async Task<IActionResult> ComplianceReportPdf(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] ComplianceReportService service)
    {
        var report = await service.BuildAsync(from, to);
        var pdf = service.RenderPdf(report);
        return File(pdf, "application/pdf",
            $"compliance-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf");
    }
}
