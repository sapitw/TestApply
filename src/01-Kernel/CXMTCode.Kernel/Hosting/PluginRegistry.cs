using System.Reflection;
using CXMTCode.Kernel.Contracts.Interfaces;
using CXMTCode.Kernel.Contracts.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Kernel.Hosting;

/// <summary>
/// 插件注册表 - 负责发现并向 PluginHost 注册插件。
/// <para>1) 静态注册：直接 RegisterAsync(IPlugin)</para>
/// <para>2) 程序集扫描：扫描已加载程序集中所有实现 IPlugin 的具体类</para>
/// <para>   先尝试通过 <see cref="IServiceProvider"/> 解析（含构造函数依赖注入），</para>
/// <para>   解析失败再回退到无参构造函数。</para>
/// </summary>
public sealed class PluginRegistry
{
    private readonly IPluginHost _host;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginRegistry>? _log;

    public PluginRegistry(IPluginHost host, IServiceProvider serviceProvider, ILogger<PluginRegistry>? log = null)
    {
        _host = host;
        _serviceProvider = serviceProvider;
        _log = log;
    }

    /// <summary>扫描程序集并注册所有 IPlugin 实现</summary>
    public async Task<Result> ScanAndRegisterAsync(IEnumerable<Assembly> assemblies)
    {
        var ok = 0;
        var fail = 0;
        var skipped = 0;

        foreach (var asm in assemblies)
        {
            IEnumerable<Type> types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                _log?.LogWarning(ex, "跳过程序集 {Asm}：ReflectionTypeLoadException", asm.FullName);
                continue;
            }

            foreach (var type in types)
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface) continue;

                IPlugin? instance = null;
                try
                {
                    // 先尝试 DI 激活（构造函数依赖会被自动解析）
                    instance = ActivatorUtilities.CreateInstance(_serviceProvider, type) as IPlugin;
                }
                catch (Exception ex)
                {
                    _log?.LogDebug(ex, "DI 实例化 {Type} 失败，尝试无参构造", type.FullName);
                }

                // 退化到无参构造
                if (instance is null && type.GetConstructor(Type.EmptyTypes) is not null)
                {
                    try { instance = (IPlugin)Activator.CreateInstance(type)!; }
                    catch (Exception ex)
                    {
                        _log?.LogError(ex, "无法实例化插件 {Type}", type.FullName);
                    }
                }

                if (instance is null) { skipped++; continue; }

                var r = await _host.RegisterAsync(instance);
                if (r.Success) ok++; else fail++;
            }
        }

        _log?.LogInformation("插件扫描完成：注册 {Ok}，失败 {Fail}，跳过 {Skipped}", ok, fail, skipped);
        return Result.Ok();
    }

    /// <summary>注册已实例化的插件集合</summary>
    public async Task<Result> RegisterAllAsync(IEnumerable<IPlugin> plugins)
    {
        foreach (var p in plugins)
        {
            var r = await _host.RegisterAsync(p);
            if (!r.Success) _log?.LogWarning("注册 {Id} 失败：{Msg}", p.PluginId, r.ErrorMessage);
        }
        return Result.Ok();
    }
}
