using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>DBA 管理接口 - 表准入、模板、白名单</summary>
[ApiController]
[Authorize(Policy = "DBA")]
[Route("api/dba")]
public sealed class DbaController : ControllerBase
{
    private readonly TableAccessRepository _access;
    private readonly DeleteTemplateRepository _templates;
    private readonly WhitelistRuleRepository _rules;
    private readonly IAuditLogger _audit;
    private readonly IUserContext _user;

    public DbaController(
        TableAccessRepository access,
        DeleteTemplateRepository templates,
        WhitelistRuleRepository rules,
        IAuditLogger audit,
        IUserContext user)
    {
        _access = access; _templates = templates; _rules = rules;
        _audit = audit; _user = user;
    }

    [HttpGet("table-access")]
    public async Task<Result<IReadOnlyList<TableAccessRecord>>> ListAccess()
        => Result<IReadOnlyList<TableAccessRecord>>.Ok(await _access.ListAsync());

    [HttpPost("table-access")]
    public async Task<Result<TableAccessRecord>> CreateAccess([FromBody] TableAccessRecord r)
    {
        r.AccessId = Guid.NewGuid().ToString("N");
        await _access.InsertAsync(r);
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "TableAccess.Create", $"申请表准入 {r.TableName}", clientIp: _user.ClientIp);
        return Result<TableAccessRecord>.Ok(r);
    }

    [HttpPost("table-access/{id}/approve")]
    public async Task<Result<bool>> ApproveAccess(string id)
    {
        var n = await _access.ApproveAsync(id, _user.UserId.ToString());
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "TableAccess.Approve", $"审批通过表准入 {id}", clientIp: _user.ClientIp);
        return Result<bool>.Ok(n > 0);
    }

    [HttpGet("templates")]
    public async Task<Result<IReadOnlyList<DeleteTemplateRecord>>> ListTemplates()
        => Result<IReadOnlyList<DeleteTemplateRecord>>.Ok(await _templates.ListAsync());

    [HttpPost("templates")]
    public async Task<Result<DeleteTemplateRecord>> CreateTemplate([FromBody] DeleteTemplateRecord r)
    {
        r.TemplateId = Guid.NewGuid().ToString("N");
        r.CreatedBy  = _user.UserId.ToString();
        await _templates.InsertAsync(r);
        return Result<DeleteTemplateRecord>.Ok(r);
    }

    [HttpPost("templates/{id}/approve")]
    public async Task<Result<bool>> ApproveTemplate(string id)
    {
        var n = await _templates.ApproveAsync(id, _user.UserId.ToString());
        return Result<bool>.Ok(n > 0);
    }

    [HttpGet("rules")]
    public async Task<Result<IReadOnlyList<WhitelistRuleRecord>>> ListRules()
        => Result<IReadOnlyList<WhitelistRuleRecord>>.Ok(await _rules.ListAsync());

    [HttpPost("rules")]
    public async Task<Result<WhitelistRuleRecord>> CreateRule([FromBody] WhitelistRuleRecord r)
    {
        r.RuleId    = Guid.NewGuid().ToString("N");
        r.CreatedBy = _user.UserId.ToString();
        await _rules.InsertAsync(r);
        return Result<WhitelistRuleRecord>.Ok(r);
    }

    [HttpDelete("rules/{id}")]
    public async Task<Result<bool>> DeleteRule(string id)
    {
        var n = await _rules.DeleteAsync(id);
        return Result<bool>.Ok(n > 0);
    }
}
