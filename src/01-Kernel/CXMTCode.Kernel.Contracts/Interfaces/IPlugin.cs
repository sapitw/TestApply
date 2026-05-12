using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>插件生命周期接口 - 所有业务插件必须实现</summary>
public interface IPlugin : IDisposable
{
    string PluginId { get; }
    string DisplayName { get; }
    string Version { get; }
    string MinKernelVersion { get; }
    IReadOnlyList<string> Dependencies { get; }

    Task<Result> InitializeAsync(IPluginContext context);
    Task<Result> StartAsync();
    Task<PluginOutput> ExecuteAsync(PluginInput input);
    Task<Result> StopAsync();
    Task<Result> UnloadAsync();
}
