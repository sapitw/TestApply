using CXMTCode.Kernel.Contracts.Interfaces;

namespace CXMTCode.Kernel.PluginLoading;

/// <summary>插件执行上下文 - 由 PluginHost 在调度时构造</summary>
public sealed class PluginContext : IPluginContext
{
    public IPermissionChecker PermissionChecker { get; }
    public IAuditLogger AuditLogger { get; }
    public IKimiTaskScheduler TaskScheduler { get; }
    public IDistributedTransaction Transaction { get; }
    public ISystemConfigService Configuration { get; }
    public IUserContext UserContext { get; }
    public IRolePermissionProvider RolePermissionProvider { get; }
    public string TraceId { get; }

    private readonly Dictionary<Type, object> _pluginConfigs = new();

    public PluginContext(
        IPermissionChecker permissionChecker,
        IAuditLogger auditLogger,
        IKimiTaskScheduler taskScheduler,
        IDistributedTransaction transaction,
        ISystemConfigService configuration,
        IUserContext userContext,
        IRolePermissionProvider rolePermissionProvider,
        string? traceId = null)
    {
        PermissionChecker = permissionChecker;
        AuditLogger = auditLogger;
        TaskScheduler = taskScheduler;
        Transaction = transaction;
        Configuration = configuration;
        UserContext = userContext;
        RolePermissionProvider = rolePermissionProvider;
        TraceId = traceId ?? Guid.NewGuid().ToString("N");
    }

    public void RegisterPluginConfig<T>(T config) where T : class, new()
        => _pluginConfigs[typeof(T)] = config;

    public T GetPluginConfig<T>() where T : class, new()
        => _pluginConfigs.TryGetValue(typeof(T), out var v) ? (T)v : new T();
}
