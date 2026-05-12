using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.PluginLoading;

/// <summary>
/// 插件抽象基类 - 所有业务插件继承此类，统一生命周期。
/// </summary>
public abstract class PluginBase : IPlugin
{
    protected IPluginContext? Context { get; private set; }
    protected IAuditLogger AuditLogger => Context!.AuditLogger;
    protected IUserContext CurrentUser => Context!.UserContext;

    public abstract string PluginId { get; }
    public abstract string DisplayName { get; }
    public abstract string Version { get; }
    public virtual string MinKernelVersion => "3.0.0";
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public virtual Task<Result> InitializeAsync(IPluginContext ctx)
    {
        Context = ctx;
        return Task.FromResult(Result.Ok());
    }

    public virtual Task<Result> StartAsync() => Task.FromResult(Result.Ok());

    public abstract Task<PluginOutput> ExecuteAsync(PluginInput input);

    public virtual Task<Result> StopAsync() => Task.FromResult(Result.Ok());

    public virtual Task<Result> UnloadAsync()
    {
        Context = null;
        return Task.FromResult(Result.Ok());
    }

    public virtual void Dispose()
    {
        Context = null;
        GC.SuppressFinalize(this);
    }
}
