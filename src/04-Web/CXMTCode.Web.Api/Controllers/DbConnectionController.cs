using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Modules.Schema.Models;
using CXMTCode.Modules.Schema.Repositories;
using CXMTCode.Plugins.Common.Adapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CXMTCode.Web.Api.Controllers;

/// <summary>DB 连接配置（DBA 专属）</summary>
[ApiController]
[Authorize(Policy = "DBA")]
[Route("api/dba/db-connections")]
public sealed class DbConnectionController : ControllerBase
{
    private readonly DbConnectionRepository _repo;
    private readonly DatabaseAdapterFactory _factory;
    private readonly IAuditLogger _audit;
    private readonly IUserContext _user;

    public DbConnectionController(
        DbConnectionRepository repo,
        DatabaseAdapterFactory factory,
        IAuditLogger audit,
        IUserContext user)
    {
        _repo = repo; _factory = factory; _audit = audit; _user = user;
    }

    [HttpGet]
    public async Task<Result<IReadOnlyList<DbConnectionRecord>>> List([FromQuery] SystemEnvironment? env)
        => Result<IReadOnlyList<DbConnectionRecord>>.Ok(await _repo.ListAsync(env));

    [HttpGet("{id}")]
    public async Task<Result<DbConnectionRecord>> Get(string id)
    {
        var c = await _repo.GetByIdAsync(id);
        return c is null
            ? Result<DbConnectionRecord>.Fail("连接不存在", "NOT_FOUND")
            : Result<DbConnectionRecord>.Ok(c);
    }

    [HttpPost]
    public async Task<Result<DbConnectionRecord>> Create([FromBody] DbConnectionUpsertDto dto)
    {
        var record = new DbConnectionRecord
        {
            ConnectionId    = Guid.NewGuid().ToString("N"),
            ConnectionName  = dto.ConnectionName,
            DatabaseType    = dto.DatabaseType,
            EnvironmentType = dto.EnvironmentType,
            Host            = dto.Host,
            Port            = dto.Port,
            ServiceName     = dto.ServiceName,
            PdbName         = dto.PdbName,
            Username        = dto.Username,
            PasswordEnc     = DbConnectionRepository.EncryptPassword(dto.Password),
            StandbyHost     = dto.StandbyHost,
            StandbyPort     = dto.StandbyPort,
            StandbyService  = dto.StandbyService,
            Description     = dto.Description,
            CreatedBy       = _user.UserId.ToString()
        };
        await _repo.InsertAsync(record);
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "DbConnection.Create", $"新增 DB 连接 {record.ConnectionName}",
            clientIp: _user.ClientIp);
        return Result<DbConnectionRecord>.Ok(record);
    }

    [HttpPut("{id}")]
    public async Task<Result<DbConnectionRecord>> Update(string id, [FromBody] DbConnectionUpsertDto dto)
    {
        var record = await _repo.GetByIdAsync(id);
        if (record is null) return Result<DbConnectionRecord>.Fail("连接不存在", "NOT_FOUND");

        record.ConnectionName  = dto.ConnectionName;
        record.DatabaseType    = dto.DatabaseType;
        record.EnvironmentType = dto.EnvironmentType;
        record.Host            = dto.Host;
        record.Port            = dto.Port;
        record.ServiceName     = dto.ServiceName;
        record.PdbName         = dto.PdbName;
        record.Username        = dto.Username;
        if (!string.IsNullOrEmpty(dto.Password))
            record.PasswordEnc = DbConnectionRepository.EncryptPassword(dto.Password);
        record.StandbyHost     = dto.StandbyHost;
        record.StandbyPort     = dto.StandbyPort;
        record.StandbyService  = dto.StandbyService;
        record.Description     = dto.Description;
        record.UpdatedBy       = _user.UserId.ToString();
        await _repo.UpdateAsync(record);

        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "DbConnection.Update", $"编辑 DB 连接 {record.ConnectionName}",
            clientIp: _user.ClientIp);
        return Result<DbConnectionRecord>.Ok(record);
    }

    [HttpDelete("{id}")]
    public async Task<Result<bool>> Delete(string id)
    {
        var n = await _repo.DeleteAsync(id);
        await _audit.WriteAsync(_user.UserId, _user.UserName, _user.Role,
            "DbConnection.Delete", $"删除 DB 连接 {id}", clientIp: _user.ClientIp);
        return Result<bool>.Ok(n > 0);
    }

    [HttpPost("{id}/test")]
    public async Task<Result<ConnectionTestResult>> Test(string id)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c is null) return Result<ConnectionTestResult>.Fail("连接不存在");

        using var adapter = _factory.Create(c.DatabaseType);
        var pwd = DbConnectionRepository.DecryptPassword(c.PasswordEnc);
        var connStr = $"Host={c.Host};Port={c.Port};Service={c.ServiceName};User={c.Username};Password={pwd}";
        var r = await adapter.TestConnectionAsync(connStr);
        await _repo.RecordTestAsync(id, r.Success, r.DatabaseVersion, r.ErrorMessage);
        return Result<ConnectionTestResult>.Ok(r);
    }

    [HttpPost("{id}/activate")]
    public async Task<Result<bool>> Activate(string id)
    {
        var n = await _repo.SetActiveEnvAsync(id);
        return Result<bool>.Ok(n > 0);
    }
}

public class DbConnectionUpsertDto
{
    public string ConnectionName { get; set; } = "";
    public DatabaseType DatabaseType { get; set; }
    public SystemEnvironment EnvironmentType { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string? ServiceName { get; set; }
    public string? PdbName { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? StandbyHost { get; set; }
    public int? StandbyPort { get; set; }
    public string? StandbyService { get; set; }
    public string? Description { get; set; }
}
