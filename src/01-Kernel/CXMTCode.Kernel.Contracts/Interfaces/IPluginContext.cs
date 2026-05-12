namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>插件执行上下文 - 插件访问内核服务的统一入口</summary>
public interface IPluginContext
{
    IPermissionChecker PermissionChecker { get; }
    IAuditLogger AuditLogger { get; }
    IKimiTaskScheduler TaskScheduler { get; }
    IDistributedTransaction Transaction { get; }
    ISystemConfigService Configuration { get; }
    IUserContext UserContext { get; }
    IRolePermissionProvider RolePermissionProvider { get; }

    T GetPluginConfig<T>() where T : class, new();
    string TraceId { get; }
}
