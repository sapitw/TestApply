using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.Common.Execution;

/// <summary>
/// D1 SQL 预审插件 - 触发链：B1 → B2 → B3 → B4 → B5 → D1。
/// 把 SQL 提交给目标 DB 的 DryRunAsync，落地 DryRunResult 到变更申请。
/// </summary>
public class SqlDryRunPlugin : PluginBase
{
    private readonly DatabaseAdapterFactory _factory;
    public SqlDryRunPlugin(DatabaseAdapterFactory factory) { _factory = factory; }
    public SqlDryRunPlugin() { _factory = new DatabaseAdapterFactory(); }

    public override string PluginId => "CXMTCode.Plugins.Common.SqlDryRun";
    public override string DisplayName => "D1 SQL 预审执行";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var sql           = input.GetParameter<string>("sql") ?? "";
        var dbType        = input.GetParameter<DatabaseType>("databaseType");
        var standbyConn   = input.GetParameter<string>("standbyConnectionString") ?? "";
        var userRole      = input.GetParameter<UserRole>("userRole");
        try
        {
            using var adapter = _factory.Create(dbType);
            var result = await adapter.DryRunAsync(sql, standbyConn, userRole);
            return PluginOutput.Ok(result);
        }
        catch (Exception ex)
        {
            return PluginOutput.Fail(ex.Message);
        }
    }
}

/// <summary>D2 影响行数预估插件 - 由 D1 的 EXPLAIN 结果提取</summary>
public class AffectedRowsEstimatePlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.AffectedRowsEstimate";
    public override string DisplayName => "D2 影响行数预估";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var dryRun = input.GetParameter<DryRunResult>("dryRunResult");
        var rows   = dryRun?.AffectedRows ?? 0;
        var level  = rows switch
        {
            < 100        => RiskLevel.Low,
            < 1000       => RiskLevel.Medium,
            < 100_000    => RiskLevel.High,
            _            => RiskLevel.Critical
        };
        return Task.FromResult(PluginOutput.Ok(new { Rows = rows, RiskLevel = level }));
    }
}

/// <summary>D3 备份脚本生成插件</summary>
public class BackupSqlGeneratorPlugin : PluginBase
{
    private readonly DatabaseAdapterFactory _factory;
    public BackupSqlGeneratorPlugin(DatabaseAdapterFactory factory) { _factory = factory; }
    public BackupSqlGeneratorPlugin() { _factory = new DatabaseAdapterFactory(); }

    public override string PluginId => "CXMTCode.Plugins.Common.BackupGenerator";
    public override string DisplayName => "D3 备份脚本生成";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var dbType    = input.GetParameter<DatabaseType>("databaseType");
        var connStr   = input.GetParameter<string>("connectionString") ?? "";
        var tableName = input.GetParameter<string>("tableName") ?? "";
        var bakName   = $"{tableName}_BAK_{DateTime.UtcNow:yyyyMMddHHmmss}";
        using var adapter = _factory.Create(dbType);
        var result = await adapter.CreateBackupAsync(tableName, connStr, bakName);
        return PluginOutput.Ok(result);
    }
}

/// <summary>D4 SQL 执行插件 - 真正在主库执行（已通过审批的变更申请）</summary>
public class SqlExecutionPlugin : PluginBase
{
    private readonly DatabaseAdapterFactory _factory;
    public SqlExecutionPlugin(DatabaseAdapterFactory factory) { _factory = factory; }
    public SqlExecutionPlugin() { _factory = new DatabaseAdapterFactory(); }

    public override string PluginId => "CXMTCode.Plugins.Common.SqlExecution";
    public override string DisplayName => "D4 SQL 真实执行";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var sql      = input.GetParameter<string>("sql") ?? "";
        var dbType   = input.GetParameter<DatabaseType>("databaseType");
        var connStr  = input.GetParameter<string>("connectionString") ?? "";
        var role     = input.GetParameter<UserRole>("userRole");
        var timeout  = input.GetParameter<int>("timeoutSeconds");
        if (timeout <= 0) timeout = 300;
        using var adapter = _factory.Create(dbType);
        var result = await adapter.ExecuteAsync(sql, connStr, role, timeout);
        return PluginOutput.Ok(result);
    }
}

/// <summary>D5 回滚 SQL 生成插件 - 基于备份表反向生成 DML</summary>
public class RollbackSqlGeneratorPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.RollbackGenerator";
    public override string DisplayName => "D5 回滚 SQL 生成";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var ctx = new Rollback.GenerationContext
        {
            OriginalSql        = input.GetParameter<string>("sql") ?? "",
            TargetTable        = input.GetParameter<string>("targetTable") ?? "",
            BackupTable        = input.GetParameter<string>("backupTable") ?? "",
            DatabaseType       = input.GetParameter<DatabaseType>("databaseType"),
            PrimaryKeyColumns  = input.GetParameter<List<string>>("primaryKeyColumns"),
            AllColumns         = input.GetParameter<List<string>>("allColumns")
        };
        var result = Rollback.RollbackSqlGenerator.Generate(ctx);
        return Task.FromResult(PluginOutput.Ok(result));
    }
}

/// <summary>D6 回滚执行插件 - 在主库执行 D5 生成的回滚 SQL</summary>
public class RollbackExecutionPlugin : PluginBase
{
    private readonly DatabaseAdapterFactory _factory;
    public RollbackExecutionPlugin(DatabaseAdapterFactory factory) { _factory = factory; }
    public RollbackExecutionPlugin() { _factory = new DatabaseAdapterFactory(); }

    public override string PluginId => "CXMTCode.Plugins.Common.RollbackExecution";
    public override string DisplayName => "D6 回滚执行";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var rollbackSql = input.GetParameter<string>("rollbackSql") ?? "";
        var dbType      = input.GetParameter<DatabaseType>("databaseType");
        var connStr     = input.GetParameter<string>("connectionString") ?? "";
        var role        = input.GetParameter<UserRole>("userRole");
        using var adapter = _factory.Create(dbType);
        var result = await adapter.ExecuteAsync(rollbackSql, connStr, role, 300);
        return PluginOutput.Ok(result);
    }
}

/// <summary>D7 执行结果通知插件 - 通过 EmailNotificationService 推送邮件（可配置）</summary>
public class ExecutionNotificationPlugin : PluginBase
{
    private readonly Notification.EmailNotificationService? _email;
    public ExecutionNotificationPlugin(Notification.EmailNotificationService email) { _email = email; }
    public ExecutionNotificationPlugin() { _email = null; }

    public override string PluginId => "CXMTCode.Plugins.Common.ExecutionNotification";
    public override string DisplayName => "D7 执行结果通知";
    public override string Version => "3.0.0";

    public override async Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var subject = input.GetParameter<string>("subject") ?? "执行结果";
        var content = input.GetParameter<string>("content") ?? "";
        var recipients = input.GetParameter<List<string>>("recipients") ?? new();

        if (_email is null || recipients.Count == 0)
            return PluginOutput.Ok(new { Sent = false, Reason = "EmailService 未注入或收件人为空", Subject = subject });

        var result = await _email.SendAsync(recipients, subject, content);
        return PluginOutput.Ok(new
        {
            Sent     = result.Success,
            Skipped  = result.Skipped,
            Message  = result.Message,
            Subject  = subject,
            Receivers = recipients.Count
        });
    }
}
