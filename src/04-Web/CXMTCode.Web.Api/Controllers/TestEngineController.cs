using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.TestEngine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>测试管理接口（SysAdmin 专属）- 套件 / 用例 / 执行</summary>
[ApiController]
[Authorize(Policy = "SysAdmin")]
[Route("api/admin/test")]
public sealed class TestEngineController : ControllerBase
{
    private readonly TestSuiteService _service;
    private readonly IUserContext _user;
    private readonly IAuditLogger _audit;

    public TestEngineController(TestSuiteService service, IUserContext user, IAuditLogger audit)
    {
        _service = service; _user = user; _audit = audit;
    }

    [HttpGet("suites")]
    public async Task<Result<IReadOnlyList<TestSuiteDto>>> ListSuites()
        => Result<IReadOnlyList<TestSuiteDto>>.Ok(await _service.ListSuitesAsync());

    [HttpPost("suites")]
    public async Task<Result<string>> CreateSuite([FromBody] TestSuiteDto dto)
    {
        var id = await _service.CreateSuiteAsync(dto, _user.UserId.ToString());
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "Test.Suite.Create", $"创建测试套件 {dto.SuiteName}", clientIp: _user.ClientIp);
        return Result<string>.Ok(id);
    }

    [HttpGet("suites/{id}/cases")]
    public async Task<Result<IReadOnlyList<TestCaseDto>>> ListCases(string id)
        => Result<IReadOnlyList<TestCaseDto>>.Ok(await _service.ListCasesAsync(id));

    [HttpPost("suites/{id}/cases")]
    public async Task<Result<string>> AddCase(string id, [FromBody] TestCaseDto dto)
    {
        dto.SuiteId = id;
        var caseId = await _service.AddCaseAsync(dto);
        return Result<string>.Ok(caseId);
    }

    [HttpPost("suites/{id}/run")]
    public async Task<Result<TestRunDto>> Run(string id)
    {
        var run = await _service.TriggerRunAsync(id, _user.UserId.ToString());
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "Test.Suite.Run", $"运行套件 {id} 通过率 {run.PassRate:P}", clientIp: _user.ClientIp);
        return Result<TestRunDto>.Ok(run);
    }

    [HttpGet("runs")]
    public async Task<Result<IReadOnlyList<TestRunDto>>> ListRuns([FromQuery] string? suiteId)
        => Result<IReadOnlyList<TestRunDto>>.Ok(await _service.ListRunsAsync(suiteId, 100));

    [HttpGet("suites/{id}/gate")]
    public async Task<Result<bool>> CheckGate(string id)
        => Result<bool>.Ok(await _service.CheckGateAsync(id));
}
