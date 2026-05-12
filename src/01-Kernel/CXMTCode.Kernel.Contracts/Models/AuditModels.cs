using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Kernel.Contracts.Models;

public class AuditLogWriteResult
{
    public bool Success { get; set; }
    public long LogId { get; set; }
    public string? HashCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AuditLogEntry
{
    public long LogId { get; set; }
    public DateTime LogTime { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public UserRole UserRole { get; set; }
    public SystemEnvironment? EnvironmentType { get; set; }
    public string OperationType { get; set; } = "";
    public string OperationDesc { get; set; } = "";
    public string? TargetDatabase { get; set; }
    public string? TargetTable { get; set; }
    public string? SqlStatement { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? ClientIp { get; set; }
    public string Result { get; set; } = "SUCCESS";
    public string HashValue { get; set; } = "";
    public string? PrevHash { get; set; }
}
