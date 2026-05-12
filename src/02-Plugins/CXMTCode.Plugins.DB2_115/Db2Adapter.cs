using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.DB2_115;

/// <summary>
/// C4 IBM Db2 11.5~12.1.4 适配插件。
/// <para>
/// 生产实现使用 IBM.Data.DB2.Core 3.x（需 Linux 原生 clidriver），关键点：
///   - 预演：HADR STANDBY 节点（<c>SELECT hadr_role FROM TABLE(sysproc.mon_get_hadr(NULL))</c>）
///   - 执行计划：<c>EXPLAIN ALL FOR ...</c>
///   - 备份：<c>CREATE TABLE bak LIKE t; INSERT INTO bak SELECT * FROM t</c>
/// </para>
/// </summary>
public sealed class Db2Adapter : AdapterStubBase
{
    public override DatabaseType DatabaseType => DatabaseType.Db2_115;
}
