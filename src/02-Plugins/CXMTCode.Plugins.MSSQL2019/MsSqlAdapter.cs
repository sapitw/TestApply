using System.Diagnostics;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;
using Microsoft.Data.SqlClient;

namespace CXMTCode.Plugins.MSSQL2019;

/// <summary>C2 SQL Server 2019 适配 - 使用 Microsoft.Data.SqlClient</summary>
public sealed class MsSqlAdapter : IDatabaseAdapter
{
    public DatabaseType DatabaseType => DatabaseType.MsSql2019;

    public async Task<ConnectionTestResult> TestConnectionAsync(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT SERVERPROPERTY('ProductVersion'), @@VERSION", conn);
            using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            var prodVer = r.GetValue(0)?.ToString() ?? "";
            var fullVer = r.GetValue(1)?.ToString() ?? "";
            sw.Stop();
            var ok = int.TryParse(prodVer.Split('.')[0], out var major) && major >= 15;
            return new ConnectionTestResult
            {
                Success         = ok,
                DatabaseVersion = $"SQL Server {prodVer}",
                LatencyMs       = (int)sw.ElapsedMilliseconds,
                ErrorMessage    = ok ? null : $"需要 SQL Server 2019 (15+)，当前 {prodVer}"
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
        if (!standbyConn.Contains("ApplicationIntent=ReadOnly", StringComparison.OrdinalIgnoreCase))
            return new DryRunResult { Success = false, ErrorMessage = "【安全拦截】SQL Server 预演必须 ApplicationIntent=ReadOnly" };

        try
        {
            using var conn = new SqlConnection(standbyConn);
            await conn.OpenAsync();
            using (var iso = new SqlCommand("SET TRANSACTION ISOLATION LEVEL SNAPSHOT", conn))
                await iso.ExecuteNonQueryAsync();

            string planXml;
            using (var on = new SqlCommand("SET SHOWPLAN_XML ON", conn))
                await on.ExecuteNonQueryAsync();
            using (var planCmd = new SqlCommand(sql, conn))
            {
                using var pr = await planCmd.ExecuteReaderAsync();
                await pr.ReadAsync();
                planXml = pr.GetValue(0)?.ToString() ?? "";
            }
            using (var off = new SqlCommand("SET SHOWPLAN_XML OFF", conn))
                await off.ExecuteNonQueryAsync();

            // 影响行数预估
            long rows = 0;
            try
            {
                using var cnt = new SqlCommand($"SELECT COUNT_BIG(*) FROM ({sql}) AS t OPTION(RECOMPILE)", conn);
                rows = Convert.ToInt64(await cnt.ExecuteScalarAsync());
            }
            catch { /* 非 SELECT 查询无法包裹，忽略 */ }

            return new DryRunResult
            {
                Success      = true,
                AffectedRows = rows,
                Plan         = new ExecutionPlan { PlanText = planXml, EstimatedRows = rows }
            };
        }
        catch (Exception ex)
        {
            return new DryRunResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ExecutionPlan> GetExecutionPlanAsync(string sql, string connStr)
    {
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using (var on = new SqlCommand("SET SHOWPLAN_XML ON", conn)) await on.ExecuteNonQueryAsync();
        using var cmd = new SqlCommand(sql, conn);
        using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        var xml = r.GetValue(0)?.ToString() ?? "";
        return new ExecutionPlan { PlanText = xml };
    }

    public async Task<ExecutionResult> ExecuteAsync(string sql, string connStr, UserRole userRole, int timeoutSeconds = 300)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = timeoutSeconds };
            var rows = await cmd.ExecuteNonQueryAsync();
            tx.Commit();
            sw.Stop();
            return new ExecutionResult { Success = true, AffectedRows = rows, ExecutionTime = sw.Elapsed };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, ErrorMessage = $"MSSQL 执行失败（ADR 已回滚）: {ex.Message}", ExecutionTime = sw.Elapsed };
        }
    }

    public Task<RollbackSqlResult> GenerateRollbackSqlAsync(string sql, string connStr) =>
        Task.FromResult(new RollbackSqlResult { Success = false, ErrorMessage = "请使用 Plugins.Common.Rollback.RollbackSqlGenerator" });

    public async Task<BackupResult> CreateBackupAsync(string table, string connStr, string backupName)
    {
        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using (var ddl = new SqlCommand($"SELECT * INTO {backupName} FROM {table}", conn))
                await ddl.ExecuteNonQueryAsync();
            using var cnt = new SqlCommand($"SELECT COUNT_BIG(*) FROM {backupName}", conn);
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
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT @@VERSION", conn);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "unknown";
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
