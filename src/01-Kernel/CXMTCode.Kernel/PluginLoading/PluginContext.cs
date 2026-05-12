using CXMTCode.Kernel.Contracts.Interfaces;

namespace CXMTCode.Kernel.PluginLoading;

/// <summary>
/// 插件执行上下文 - 由 PluginHost 在调度时构造。
/// 大多数依赖直接注入；<see cref="UserContext"/> 通过工厂延迟解析，避免
/// 在启动 / 单例作用域中触发 scoped 服务解析异常。
/// </summary>
public sealed class PluginContext : IPluginContext
{
    public IPermissionChecker PermissionChecker { get; }
    public IAuditLogger AuditLogger { get; }
    public IKimiTaskScheduler TaskScheduler { get; }
    public IDistributedTransaction Transaction { get; }
    public ISystemConfigService Configuration { get; }
    public IRolePermissionProvider RolePermissionProvider { get; }
    public string TraceId { get; }

    private readonly Func<IUserContext> _userContextResolver;
    public IUserContext UserContext
    {
        get
        {
            try { return _userContextResolver(); }
            catch { return CXMTCode.Kernel.Security.UserContext.Anonymous(); }
        }
    }

    private readonly Dictionary<Type, object> _pluginConfigs = new();

    public PluginContext(
        IPermissionChecker permissionChecker,
        IAuditLogger auditLogger,
        IKimiTaskScheduler taskScheduler,
        IDistributedTransaction transaction,
        ISystemConfigService configuration,
        Func<IUserContext> userContextResolver,
        IRolePermissionProvider rolePermissionProvider,
        string? traceId = null)
    {
        PermissionChecker = permissionChecker;
        AuditLogger = auditLogger;
        TaskScheduler = taskScheduler;
        Transaction = transaction;
        Configuration = configuration;
        _userContextResolver = userContextResolver;
        RolePermissionProvider = rolePermissionProvider;
        TraceId = traceId ?? Guid.NewGuid().ToString("N");
    }

    public void RegisterPluginConfig<T>(T config) where T : class, new()
        => _pluginConfigs[typeof(T)] = config;

    public T GetPluginConfig<T>() where T : class, new()
        => _pluginConfigs.TryGetValue(typeof(T), out var v) ? (T)v : new T();
}
