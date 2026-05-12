using System.Reflection;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Kernel.Hosting;

/// <summary>
/// 插件注册表 - 负责发现并向 PluginHost 注册插件。
/// <para>1) 静态注册：直接 RegisterAsync(IPlugin)</para>
/// <para>2) 程序集扫描：扫描已加载程序集中所有实现 IPlugin 的具体类，反射创建实例</para>
/// </summary>
public sealed class PluginRegistry
{
    private readonly IPluginHost _host;
    private readonly ILogger<PluginRegistry>? _log;

    public PluginRegistry(IPluginHost host, ILogger<PluginRegistry>? log = null)
    {
        _host = host;
        _log  = log;
    }

    /// <summary>扫描程序集并注册所有 IPlugin 实现</summary>
    public async Task<Result> ScanAndRegisterAsync(IEnumerable<Assembly> assemblies)
    {
        var ok = 0;
        var fail = 0;

        foreach (var asm in assemblies)
        {
            IEnumerable<Type> types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                _log?.LogWarning(ex, "Skip assembly {Asm} due to ReflectionTypeLoadException", asm.FullName);
                continue;
            }

            foreach (var type in types)
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface) continue;
                if (type.GetConstructor(Type.EmptyTypes) is null) continue;

                try
                {
                    var instance = (IPlugin)Activator.CreateInstance(type)!;
                    var r = await _host.RegisterAsync(instance);
                    if (r.Success) ok++; else fail++;
                }
                catch (Exception ex)
                {
                    fail++;
                    _log?.LogError(ex, "Failed to instantiate plugin {Type}", type.FullName);
                }
            }
        }

        return Result.Ok();
    }

    /// <summary>注册已实例化的插件集合</summary>
    public async Task<Result> RegisterAllAsync(IEnumerable<IPlugin> plugins)
    {
        foreach (var p in plugins)
        {
            var r = await _host.RegisterAsync(p);
            if (!r.Success) _log?.LogWarning("Register {Id} failed: {Msg}", p.PluginId, r.ErrorMessage);
        }
        return Result.Ok();
    }
}
