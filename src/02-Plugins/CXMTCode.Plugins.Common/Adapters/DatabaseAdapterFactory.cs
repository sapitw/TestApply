using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Plugins.Common.Adapters;

/// <summary>
/// 数据库适配器工厂 - 由 DI 注入具体实现，避免编译时硬依赖各适配器项目。
/// 默认通过 <see cref="Register"/> 在启动期登记每种类型对应的工厂方法。
/// </summary>
public sealed class DatabaseAdapterFactory
{
    private readonly Dictionary<DatabaseType, Func<IDatabaseAdapter>> _factories = new();

    public DatabaseAdapterFactory Register(DatabaseType type, Func<IDatabaseAdapter> factory)
    {
        _factories[type] = factory;
        return this;
    }

    public IDatabaseAdapter Create(DatabaseType type)
    {
        if (!_factories.TryGetValue(type, out var factory))
            throw new NotSupportedException($"数据库类型 {type} 未注册适配器");
        return factory();
    }

    public IReadOnlyCollection<DatabaseType> SupportedTypes => _factories.Keys.ToList();
}
