using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.MSSQL2019;

/// <summary>
/// C2 SQL Server 2019 适配插件。
/// <para>
/// 生产实现使用 Microsoft.Data.SqlClient 5.x，关键点：
///   - 预演：连接串必须包含 <c>ApplicationIntent=ReadOnly</c>（AlwaysOn 只读副本）
///   - 隔离级别：<c>SET TRANSACTION ISOLATION LEVEL SNAPSHOT</c>
///   - 执行计划：<c>SET SHOWPLAN_XML ON</c>
/// </para>
/// </summary>
public sealed class MsSqlAdapter : AdapterStubBase
{
    public override DatabaseType DatabaseType => DatabaseType.MsSql2019;

    public override Task<DryRunResult> DryRunAsync(string sql, string standbyConnectionString, UserRole userRole)
    {
        if (!standbyConnectionString.Contains("ApplicationIntent=ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new DryRunResult
            {
                Success = false,
                ErrorMessage = "【安全拦截】SQL Server 预演必须设置 ApplicationIntent=ReadOnly"
            });
        }
        return base.DryRunAsync(sql, standbyConnectionString, userRole);
    }
}
