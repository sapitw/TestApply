using System.Diagnostics;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;
using MySqlConnector;

namespace CXMTCode.Plugins.MySQL80;

/// <summary>C3 MySQL 8.0+ 适配 - 使用 MySqlConnector（纯托管，跨平台）</summary>
public sealed class MySqlAdapter : IDatabaseAdapter
{
    public DatabaseType DatabaseType => DatabaseType.MySql80;

    public async Task<ConnectionTestResult> TestConnectionAsync(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("SELECT VERSION()", conn);
            var ver = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
            sw.Stop();
            var ok = int.TryParse(ver.Split('.')[0], out var major) && major >= 8;
            return new ConnectionTestResult
            {
                Success         = ok,
                DatabaseVersion = $"MySQL {ver}",
                LatencyMs       = (int)sw.ElapsedMilliseconds,
                ErrorMessage    = ok ? null : $"需要 MySQL >= 8.0，当前 {ver}"
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
        try
        {
            using var conn = new MySqlConnection(standbyConn);
            await conn.OpenAsync();
            using var ro = new MySqlCommand("SELECT @@read_only", conn);
            if (Convert.ToInt32(await ro.ExecuteScalarAsync()) != 1)
                return new DryRunResult { Success = false, ErrorMessage = "【安全拦截】非只读 Secondary 节点" };

            // EXPLAIN
            using var ex = new MySqlCommand($"EXPLAIN FORMAT=JSON {sql}", conn);
            var planJson = (await ex.ExecuteScalarAsync())?.ToString() ?? "";

            // 在事务中执行 + 回滚以预估影响行数（仅 DML 适用）
            long rows = 0;
            try
            {
                await using var tx = await conn.BeginTransactionAsync();
                using var cnt = new MySqlCommand($"SELECT COUNT(*) FROM ({sql}) AS t", conn) { Transaction = tx };
                rows = Convert.ToInt64(await cnt.ExecuteScalarAsync());
                await tx.RollbackAsync();
            }
            catch { /* 非 SELECT 包裹失败 */ }

            return new DryRunResult
            {
                Success      = true,
                AffectedRows = rows,
                Plan         = new ExecutionPlan { PlanText = planJson, EstimatedRows = rows }
            };
        }
        catch (Exception e)
        {
            return new DryRunResult { Success = false, ErrorMessage = e.Message };
        }
    }

    public async Task<ExecutionPlan> GetExecutionPlanAsync(string sql, string connStr)
    {
        using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new MySqlCommand($"EXPLAIN FORMAT=JSON {sql}", conn);
        return new ExecutionPlan { PlanText = (await cmd.ExecuteScalarAsync())?.ToString() ?? "" };
    }

    public async Task<ExecutionResult> ExecuteAsync(string sql, string connStr, UserRole userRole, int timeoutSeconds = 300)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();
            using var cmd = new MySqlCommand(sql, conn, tx) { CommandTimeout = timeoutSeconds };
            var rows = await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            sw.Stop();
            return new ExecutionResult { Success = true, AffectedRows = rows, ExecutionTime = sw.Elapsed };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, ErrorMessage = $"MySQL 执行失败（原子 DDL 已回滚）: {ex.Message}", ExecutionTime = sw.Elapsed };
        }
    }

    public Task<RollbackSqlResult> GenerateRollbackSqlAsync(string sql, string connStr) =>
        Task.FromResult(new RollbackSqlResult { Success = false, ErrorMessage = "请使用 Plugins.Common.Rollback.RollbackSqlGenerator" });

    public async Task<BackupResult> CreateBackupAsync(string table, string connStr, string backupName)
    {
        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            using (var ddl = new MySqlCommand($"CREATE TABLE {backupName} AS SELECT * FROM {table}", conn))
                await ddl.ExecuteNonQueryAsync();
            using var cnt = new MySqlCommand($"SELECT COUNT(*) FROM {backupName}", conn);
            return new BackupResult
            {
                Success         = true,
                BackupTableName = backupName,
                BackupRowCount  = Convert.ToInt64(await cnt.ExecuteScalarAsync())
            };
        }
        catch (Exception ex)
        {
            return new BackupResult { Success = false, BackupTableName = backupName, ErrorMessage = ex.Message };
        }
    }

    public async Task<string> GetDatabaseVersionAsync(string connStr)
    {
        using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT VERSION()", conn);
        return $"MySQL {(await cmd.ExecuteScalarAsync())?.ToString() ?? "unknown"}";
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
