using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>插件宿主 - 管理插件加载、查找、调度</summary>
public interface IPluginHost
{
    Task<Result> RegisterAsync(IPlugin plugin);
    Task<Result> UnregisterAsync(string pluginId);

    IPlugin? Find(string pluginId);
    IReadOnlyList<IPlugin> ListPlugins();
    IReadOnlyDictionary<string, PluginStatus> GetStatuses();

    Task<PluginOutput> InvokeAsync(string pluginId, PluginInput input);

    /// <summary>启动全部插件</summary>
    Task<Result> StartAllAsync();
    Task<Result> StopAllAsync();
}
