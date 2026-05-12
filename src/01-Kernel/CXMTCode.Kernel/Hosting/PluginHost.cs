using System.Collections.Concurrent;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Kernel.Hosting;

/// <summary>
/// 插件宿主 - 管理插件生命周期和调度。
/// 内部使用 PluginRegistry 跟踪插件状态，TraceId 隔离每次调用。
/// </summary>
public sealed class PluginHost : IPluginHost
{
    private readonly ConcurrentDictionary<string, IPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginStatus> _statuses = new();
    private readonly PluginContextFactory _contextFactory;
    private readonly ILogger<PluginHost>? _log;

    public PluginHost(PluginContextFactory contextFactory, ILogger<PluginHost>? log = null)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    public async Task<Result> RegisterAsync(IPlugin plugin)
    {
        if (!_plugins.TryAdd(plugin.PluginId, plugin))
            return Result.Fail($"插件 {plugin.PluginId} 已注册", "PLUGIN_DUP");

        _statuses[plugin.PluginId] = PluginStatus.Loaded;
        var ctx = _contextFactory.Create();
        var init = await plugin.InitializeAsync(ctx);
        if (!init.Success)
        {
            _statuses[plugin.PluginId] = PluginStatus.Failed;
            return init;
        }
        _statuses[plugin.PluginId] = PluginStatus.Initialized;
        _log?.LogInformation("Plugin {PluginId} registered & initialized", plugin.PluginId);
        return Result.Ok();
    }

    public async Task<Result> UnregisterAsync(string pluginId)
    {
        if (!_plugins.TryRemove(pluginId, out var plugin)) return Result.Fail("插件不存在");
        try
        {
            await plugin.StopAsync();
            await plugin.UnloadAsync();
            plugin.Dispose();
            _statuses[pluginId] = PluginStatus.Unloaded;
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _statuses[pluginId] = PluginStatus.Failed;
            return Result.Fail(ex.Message);
        }
    }

    public IPlugin? Find(string pluginId) => _plugins.TryGetValue(pluginId, out var p) ? p : null;

    public IReadOnlyList<IPlugin> ListPlugins() => _plugins.Values.ToList();

    public IReadOnlyDictionary<string, PluginStatus> GetStatuses() =>
        new Dictionary<string, PluginStatus>(_statuses);

    public async Task<PluginOutput> InvokeAsync(string pluginId, PluginInput input)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
            return PluginOutput.Fail($"插件 {pluginId} 未注册");
        try
        {
            return await plugin.ExecuteAsync(input);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Plugin {Id} execute failed", pluginId);
            return PluginOutput.Fail(ex.Message);
        }
    }

    public async Task<Result> StartAllAsync()
    {
        foreach (var p in _plugins.Values)
        {
            var r = await p.StartAsync();
            _statuses[p.PluginId] = r.Success ? PluginStatus.Started : PluginStatus.Failed;
        }
        return Result.Ok();
    }

    public async Task<Result> StopAllAsync()
    {
        foreach (var p in _plugins.Values)
        {
            await p.StopAsync();
            _statuses[p.PluginId] = PluginStatus.Stopped;
        }
        return Result.Ok();
    }
}
