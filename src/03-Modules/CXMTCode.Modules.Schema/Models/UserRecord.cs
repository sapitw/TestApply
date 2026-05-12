using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Modules.Schema.Models;

public class UserRecord
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.User;
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DbConnectionRecord
{
    public string ConnectionId { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public DatabaseType DatabaseType { get; set; }
    public SystemEnvironment EnvironmentType { get; set; }
    public bool IsActiveEnv { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string? ServiceName { get; set; }
    public string? PdbName { get; set; }
    public string Username { get; set; } = "";
    public string PasswordEnc { get; set; } = "";
    public string? ConnectionStringEnc { get; set; }
    public string? StandbyHost { get; set; }
    public int? StandbyPort { get; set; }
    public string? StandbyService { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsStandby { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TestResult { get; set; }
    public DateTime? TestedAt { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ChangeRequestRecord
{
    public string RequestId { get; set; } = "";
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Draft;
    public SqlOperationType OperationType { get; set; }
    public DatabaseType DatabaseType { get; set; }
    public string ConnectionId { get; set; } = "";
    public string TargetTable { get; set; } = "";
    public string SqlStatement { get; set; } = "";
    public string? SqlHash { get; set; }
    public string Reason { get; set; } = "";
    public int ImpactLevel { get; set; } = 1;
    public long? AffectedRows { get; set; }
    public string ApplicantId { get; set; } = "";
    public UserRole ApplicantRole { get; set; } = UserRole.User;
    public string? ApproverId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DryRunResult { get; set; }
    public string? BackupTable { get; set; }
    public string? RollbackSql { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? ExecutedBy { get; set; }
    public string? ExecResult { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? RolledBackBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class TableAccessRecord
{
    public string AccessId { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? TableSchema { get; set; }
    public int Status { get; set; }
    public string? DbaApprover { get; set; }
    public string? BizApprover { get; set; }
    public bool HasPrimaryKey { get; set; }
    public bool HasUniqueIdx { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}

public class DeleteTemplateRecord
{
    public string TemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string TableName { get; set; } = "";
    public string TemplateSql { get; set; } = "";
    public string? ParametersJson { get; set; }
    public int MaxAffectedRows { get; set; } = 1000;
    public int Status { get; set; }
    public int Version { get; set; } = 1;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? FirstExecutedAt { get; set; }
}

public class WhitelistRuleRecord
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string TableName { get; set; } = "";
    public DatabaseType DatabaseType { get; set; }
    public string? AllowedColumns { get; set; }
    public int MaxAffected { get; set; } = 1000;
    public string? TimeWindow { get; set; }
    public int Status { get; set; } = 1;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
