using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Plugins.Common.Adapters;

/// <summary>
/// 数据库适配器占位基类 - 没有真实驱动时使用。
/// <para>
/// 真实部署时各 C1-C4 适配项目应继承并覆盖：
///   - C1 Oracle19c：使用 Oracle.ManagedDataAccess.Core
///   - C2 MSSQL2019：使用 Microsoft.Data.SqlClient
///   - C3 MySQL80：使用 MySqlConnector
///   - C4 DB2_115：使用 IBM.Data.DB2.Core
/// </para>
/// 当前仅返回 NotImplemented 模拟结果，便于上层流程能在没有真实 DB 时跑完整链路。
/// </summary>
public abstract class AdapterStubBase : IDatabaseAdapter
{
    public abstract DatabaseType DatabaseType { get; }

    public virtual Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
        => Task.FromResult(new ConnectionTestResult
        {
            Success = false,
            ErrorMessage = $"[STUB] {DatabaseType} 适配器尚未实现 - 缺少驱动 / 仅供编译期占位",
            DatabaseVersion = DatabaseType.GetVersionName(),
            LatencyMs = 0
        });

    public virtual Task<DryRunResult> DryRunAsync(string sql, string standbyConnectionString, UserRole userRole)
        => Task.FromResult(new DryRunResult
        {
            Success = true,
            AffectedRows = 0,
            Plan = new ExecutionPlan { PlanText = $"[STUB] DryRun for {DatabaseType}: {sql}" },
            Warnings = new() { "STUB 实现，未真实预演" }
        });

    public virtual Task<ExecutionPlan> GetExecutionPlanAsync(string sql, string connectionString)
        => Task.FromResult(new ExecutionPlan { PlanText = $"[STUB] EXPLAIN for {DatabaseType}: {sql}" });

    public virtual Task<ExecutionResult> ExecuteAsync(string sql, string connectionString, UserRole userRole, int timeoutSeconds = 300)
        => Task.FromResult(new ExecutionResult
        {
            Success = false,
            AffectedRows = 0,
            ExecutionTime = TimeSpan.Zero,
            ErrorMessage = $"[STUB] {DatabaseType} 执行未实现 - 待接入真实驱动"
        });

    public virtual Task<RollbackSqlResult> GenerateRollbackSqlAsync(string originalSql, string connectionString)
        => Task.FromResult(new RollbackSqlResult
        {
            Success = false,
            ErrorMessage = "[STUB] 回滚 SQL 生成器待实现：需在变更前快照行内容，并按 PK 反向生成 INSERT/UPDATE"
        });

    public virtual Task<BackupResult> CreateBackupAsync(string tableName, string connectionString, string backupTableName)
        => Task.FromResult(new BackupResult
        {
            Success = false,
            BackupTableName = backupTableName,
            ErrorMessage = $"[STUB] {DatabaseType} 备份未实现"
        });

    public virtual Task<string> GetDatabaseVersionAsync(string connectionString)
        => Task.FromResult($"[STUB] {DatabaseType.GetVersionName()}");

    public virtual void Dispose() { GC.SuppressFinalize(this); }
}
