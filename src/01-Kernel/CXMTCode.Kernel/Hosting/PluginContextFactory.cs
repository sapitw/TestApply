using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.PluginLoading;
using Microsoft.Extensions.DependencyInjection;

namespace CXMTCode.Kernel.Hosting;

/// <summary>
/// 插件上下文工厂 - 每次调用插件时构造一份独立的 <see cref="IPluginContext"/>。
/// 通过 <see cref="IServiceProvider"/> 解析当前请求的 <see cref="IUserContext"/> 等服务。
/// </summary>
public sealed class PluginContextFactory
{
    private readonly IServiceProvider _provider;
    public PluginContextFactory(IServiceProvider provider) => _provider = provider;

    public IPluginContext Create(string? traceId = null)
    {
        return new PluginContext(
            _provider.GetRequiredService<IPermissionChecker>(),
            _provider.GetRequiredService<IAuditLogger>(),
            _provider.GetRequiredService<IKimiTaskScheduler>(),
            _provider.GetRequiredService<IDistributedTransaction>(),
            _provider.GetRequiredService<ISystemConfigService>(),
            _provider.GetRequiredService<IUserContext>(),
            _provider.GetRequiredService<IRolePermissionProvider>(),
            traceId);
    }
}
