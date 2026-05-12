using System.Diagnostics;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;
using Oracle.ManagedDataAccess.Client;

namespace CXMTCode.Plugins.Oracle19c;

/// <summary>
/// C1 Oracle 19C 适配 - 使用 Oracle.ManagedDataAccess.Core（纯托管，跨平台）。
/// 预演必须连接 ADG 只读备库（连接串包含 STANDBY/READONLY）。
/// </summary>
public sealed class OracleAdapter : IDatabaseAdapter
{
    public DatabaseType DatabaseType => DatabaseType.Oracle19c;

    public async Task<ConnectionTestResult> TestConnectionAsync(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new OracleCommand("SELECT banner FROM v$version WHERE ROWNUM=1", conn);
            var version = (await cmd.ExecuteScalarAsync())?.ToString() ?? "Oracle";
            sw.Stop();
            var ok = version.Contains("19.") || version.Contains("21.") || version.Contains("23.");
            return new ConnectionTestResult
            {
                Success = ok,
                DatabaseVersion = version,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                ErrorMessage = ok ? null : $"版本 {version} 不符合 19c+ 要求"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult { Success = false, ErrorMessage = ex.Message, LatencyMs = (int)sw.ElapsedMilliseconds };
        }
    }

    public async Task<DryRunResult> DryRunAsync(string sql, string standbyConn, UserRole userRole)
    {
        if (!standbyConn.Contains("STANDBY", StringComparison.OrdinalIgnoreCase)
            && !standbyConn.Contains("READONLY", StringComparison.OrdinalIgnoreCase))
        {
            return new DryRunResult { Success = false, ErrorMessage = "【安全拦截】Oracle 预演必须连接 ADG 只读备库" };
        }

        try
        {
            using var conn = new OracleConnection(standbyConn);
            await conn.OpenAsync();
            // 双重保险：只读事务
            using (var ro = new OracleCommand("ALTER SESSION SET TRANSACTION READ ONLY", conn))
                await ro.ExecuteNonQueryAsync();

            // 生成执行计划
            using (var explain = new OracleCommand($"EXPLAIN PLAN FOR {sql}", conn))
                await explain.ExecuteNonQueryAsync();

            var planText = await GetExplainPlanAsync(conn);
            var estRows  = await GetEstimatedRowsAsync(conn);

            return new DryRunResult
            {
                Success      = true,
                AffectedRows = estRows,
                Plan         = new ExecutionPlan { PlanText = planText, EstimatedRows = estRows }
            };
        }
        catch (Exception ex)
        {
            return new DryRunResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ExecutionPlan> GetExecutionPlanAsync(string sql, string connStr)
    {
        using var conn = new OracleConnection(connStr);
        await conn.OpenAsync();
        using (var explain = new OracleCommand($"EXPLAIN PLAN FOR {sql}", conn))
            await explain.ExecuteNonQueryAsync();
        return new ExecutionPlan
        {
            PlanText      = await GetExplainPlanAsync(conn),
            EstimatedRows = await GetEstimatedRowsAsync(conn)
        };
    }

    public async Task<ExecutionResult> ExecuteAsync(string sql, string connStr, UserRole userRole, int timeoutSeconds = 300)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            using var tx  = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            using var cmd = new OracleCommand(sql, conn) { CommandTimeout = timeoutSeconds, BindByName = true, Transaction = tx };
            var rows = await cmd.ExecuteNonQueryAsync();
            tx.Commit();
            sw.Stop();
            return new ExecutionResult { Success = true, AffectedRows = rows, ExecutionTime = sw.Elapsed };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, ErrorMessage = $"Oracle 执行失败（已自动回滚）: {ex.Message}", ExecutionTime = sw.Elapsed };
        }
    }

    public Task<RollbackSqlResult> GenerateRollbackSqlAsync(string originalSql, string connStr)
    {
        // 回滚 SQL 由 RollbackSqlGenerator（Plugins.Common）统一生成
        return Task.FromResult(new RollbackSqlResult { Success = false, ErrorMessage = "请使用 Plugins.Common.Rollback.RollbackSqlGenerator 生成回滚脚本" });
    }

    public async Task<BackupResult> CreateBackupAsync(string table, string connStr, string backupName)
    {
        try
        {
            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            using (var ddl = new OracleCommand($"CREATE TABLE {backupName} AS SELECT * FROM {table}", conn))
                await ddl.ExecuteNonQueryAsync();
            using var cnt = new OracleCommand($"SELECT COUNT(*) FROM {backupName}", conn);
            var rows = Convert.ToInt64(await cnt.ExecuteScalarAsync());
            return new BackupResult { Success = true, BackupTableName = backupName, BackupRowCount = rows };
        }
        catch (Exception ex)
        {
            return new BackupResult { Success = false, BackupTableName = backupName, ErrorMessage = ex.Message };
        }
    }

    public async Task<string> GetDatabaseVersionAsync(string connStr)
    {
        using var conn = new OracleConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new OracleCommand("SELECT banner FROM v$version WHERE ROWNUM=1", conn);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "unknown";
    }

    public void Dispose() => GC.SuppressFinalize(this);

    private static async Task<string> GetExplainPlanAsync(OracleConnection conn)
    {
        using var cmd = new OracleCommand("SELECT plan_table_output FROM TABLE(dbms_xplan.display())", conn);
        using var r = await cmd.ExecuteReaderAsync();
        var sb = new System.Text.StringBuilder();
        while (await r.ReadAsync()) sb.AppendLine(r.GetString(0));
        return sb.ToString();
    }

    private static async Task<long> GetEstimatedRowsAsync(OracleConnection conn)
    {
        using var cmd = new OracleCommand("SELECT cardinality FROM plan_table WHERE id=0", conn);
        var r = await cmd.ExecuteScalarAsync();
        return r is null or DBNull ? -1 : Convert.ToInt64(r);
    }
}
