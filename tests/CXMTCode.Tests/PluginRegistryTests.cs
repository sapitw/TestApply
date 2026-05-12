using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.Hosting;
using CXMTCode.Kernel.PluginLoading;
using Microsoft.Extensions.DependencyInjection;

namespace CXMTCode.Tests;

public class PluginRegistryTests
{
    [Fact]
    public async Task ScanAndRegister_finds_parameterless_plugins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogger, FakeAudit>();
        services.AddSingleton<IRolePermissionProvider, CXMTCode.Kernel.Security.RolePermissionProvider>();
        services.AddSingleton<IKimiTaskScheduler, CXMTCode.Kernel.Scheduling.KimiTaskScheduler>();
        services.AddSingleton<IDistributedTransaction, CXMTCode.Kernel.Transaction.DistributedTransactionManager>();
        services.AddSingleton<ISystemConfigService, FakeConfig>();
        services.AddSingleton<IUserContext>(_ => CXMTCode.Kernel.Security.UserContext.Anonymous());
        services.AddSingleton<IPermissionChecker, CXMTCode.Kernel.Security.PermissionChecker>();
        services.AddSingleton<PluginContextFactory>();
        services.AddSingleton<IPluginHost, PluginHost>();
        services.AddSingleton<PluginRegistry>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<PluginRegistry>();
        var host     = provider.GetRequiredService<IPluginHost>();

        // 扫描 B1/B2 等参数 less 插件所在程序集
        var asm = typeof(CXMTCode.Plugins.Common.Security.SqlParserPlugin).Assembly;
        await registry.ScanAndRegisterAsync(new[] { asm });

        host.Find("CXMTCode.Plugins.Common.SqlParser").Should().NotBeNull();
        host.Find("CXMTCode.Plugins.Common.AstValidator").Should().NotBeNull();
        host.Find("CXMTCode.Plugins.Common.DeleteTemplate").Should().NotBeNull();
    }

    private sealed class FakeAudit : IAuditLogger
    {
        public Task<AuditLogWriteResult> WriteAsync(Guid u, string un, CXMTCode.Kernel.Contracts.Enums.UserRole r,
            string op, string desc, string? db = null, string? tb = null, string? sql = null,
            string? before = null, string? after = null, string? ip = null, string? result = null,
            CXMTCode.Kernel.Contracts.Enums.SystemEnvironment? env = null)
            => Task.FromResult(new AuditLogWriteResult { Success = true, LogId = 1, HashCode = "fake" });
        public Task<bool> VerifyIntegrityAsync(long logId) => Task.FromResult(true);
        public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(Guid? userId = null, DateTime? from = null,
            DateTime? to = null, string? operationType = null, int pageIndex = 0, int pageSize = 50)
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(new List<AuditLogEntry>());
    }

    private sealed class FakeConfig : ISystemConfigService
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task<T?> GetAsync<T>(string key) where T : class, new() => Task.FromResult<T?>(null);
        public Task SetAsync(string key, string value, string? updatedBy = null) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key) => Task.FromResult(false);
        public Task<IReadOnlyDictionary<string, string>> GetByGroupAsync(string group)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
