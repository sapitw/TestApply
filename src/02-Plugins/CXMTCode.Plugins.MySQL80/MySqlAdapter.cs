using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.MySQL80;

/// <summary>
/// C3 MySQL 8.0+ 适配插件。
/// <para>
/// 生产实现使用 MySqlConnector 2.x，关键点：
///   - 预演：在 Group Replication Secondary 节点（<c>SELECT @@read_only = 1</c>）
///   - 执行计划：<c>EXPLAIN FORMAT=JSON ...</c>
///   - 8.0 原子 DDL：CREATE/ALTER 失败自动回滚，不需手动事务
/// </para>
/// </summary>
public sealed class MySqlAdapter : AdapterStubBase
{
    public override DatabaseType DatabaseType => DatabaseType.MySql80;
}
