using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using CXMTCode.Modules.Schema.Services;
using CXMTCode.Plugins.Common.Adapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>变更申请相关接口</summary>
[ApiController]
[Authorize]
[Route("api/change-requests")]
public sealed class ChangeRequestController : ControllerBase
{
    private readonly ChangeRequestService _service;
    private readonly ChangeRequestRepository _repo;
    private readonly IUserContext _user;

    public ChangeRequestController(
        ChangeRequestService service,
        ChangeRequestRepository repo,
        IUserContext user)
    {
        _service = service; _repo = repo; _user = user;
    }

    [HttpGet]
    public async Task<Result<IReadOnlyList<ChangeRequestRecord>>> List()
    {
        var applicant = _user.IsDbaOrAbove ? null : _user.UserId.ToString();
        return Result<IReadOnlyList<ChangeRequestRecord>>.Ok(
            await _repo.ListAsync(applicant, 0, 100));
    }

    [HttpGet("{id}")]
    public async Task<Result<ChangeRequestRecord>> Get(string id)
    {
        var r = await _repo.GetAsync(id);
        return r is null
            ? Result<ChangeRequestRecord>.Fail("申请不存在")
            : Result<ChangeRequestRecord>.Ok(r);
    }

    [HttpPost]
    public async Task<Result<SubmissionResult>> Submit([FromBody] SubmitChangeRequest req)
    {
        var r = await _service.SubmitAsync(req, _user);
        return Result<SubmissionResult>.Ok(r);
    }

    [HttpPost("{id}/dry-run")]
    public async Task<Result<DryRunResult>> DryRun(string id)
        => Result<DryRunResult>.Ok(await _service.DryRunAsync(id, _user));

    [HttpPost("{id}/execute")]
    public async Task<Result<ExecutionResult>> Execute(string id)
        => Result<ExecutionResult>.Ok(await _service.ExecuteAsync(id, _user));

    [HttpPost("{id}/rollback")]
    public async Task<Result<ExecutionResult>> Rollback(string id)
        => Result<ExecutionResult>.Ok(await _service.RollbackAsync(id, _user));

    [HttpPost("{id}/approve")]
    [Authorize(Policy = "DBA")]
    public async Task<Result<bool>> Approve(string id)
    {
        var n = await _repo.UpdateStatusAsync(id, Kernel.Contracts.Enums.ChangeRequestStatus.Approved, _user.UserId.ToString());
        return Result<bool>.Ok(n > 0);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Policy = "DBA")]
    public async Task<Result<bool>> Reject(string id)
    {
        var n = await _repo.UpdateStatusAsync(id, Kernel.Contracts.Enums.ChangeRequestStatus.Rejected, _user.UserId.ToString());
        return Result<bool>.Ok(n > 0);
    }
}
