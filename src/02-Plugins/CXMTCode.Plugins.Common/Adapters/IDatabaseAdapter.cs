using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Plugins.Common.Adapters;

/// <summary>
/// 数据库适配器统一契约 - 四大业务库（Oracle/MSSQL/MySQL/DB2）都实现此接口。
/// 设计原则：
///   - 预演必须连只读备库（ADG/AlwaysOn-ReadOnly/GR-Secondary/HADR-Standby）
///   - 真实执行必须开启事务，失败自动回滚
///   - 回滚 SQL 由业务前一步备份表生成（参见 D 系列插件）
/// </summary>
public interface IDatabaseAdapter : IDisposable
{
    DatabaseType DatabaseType { get; }

    Task<ConnectionTestResult>   TestConnectionAsync(string connectionString);
    Task<DryRunResult>           DryRunAsync(string sql, string standbyConnectionString, UserRole userRole);
    Task<ExecutionPlan>          GetExecutionPlanAsync(string sql, string connectionString);
    Task<ExecutionResult>        ExecuteAsync(string sql, string connectionString, UserRole userRole, int timeoutSeconds = 300);
    Task<RollbackSqlResult>      GenerateRollbackSqlAsync(string originalSql, string connectionString);
    Task<BackupResult>           CreateBackupAsync(string tableName, string connectionString, string backupTableName);
    Task<string>                 GetDatabaseVersionAsync(string connectionString);
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? DatabaseVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public int LatencyMs { get; set; }
}

public class DryRunResult
{
    public bool Success { get; set; }
    public long AffectedRows { get; set; }
    public ExecutionPlan? Plan { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ExecutionPlan
{
    public string PlanText { get; set; } = "";
    public long EstimatedRows { get; set; }
    public long EstimatedCost { get; set; }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public long AffectedRows { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RollbackSqlResult
{
    public bool Success { get; set; }
    public string? RollbackSql { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string BackupTableName { get; set; } = "";
    public long BackupRowCount { get; set; }
    public string? ErrorMessage { get; set; }
}
