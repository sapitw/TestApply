using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.Oracle19c;

/// <summary>
/// C1 Oracle 19C 适配插件。
/// <para>
/// 生产实现使用 Oracle.ManagedDataAccess.Core 19.x，关键点：
///   - 预演：连接串包含 "STANDBY" / "READONLY" 字样的 ADG 备库
///   - 双重保险：<c>ALTER SESSION SET TRANSACTION READ ONLY</c>
///   - 执行计划：<c>EXPLAIN PLAN FOR ... ; SELECT plan_table_output FROM TABLE(dbms_xplan.display())</c>
/// </para>
/// 此环境未安装 Oracle 驱动，使用 <see cref="AdapterStubBase"/> 占位以保证编译通过。
/// </summary>
public sealed class OracleAdapter : AdapterStubBase
{
    public override DatabaseType DatabaseType => DatabaseType.Oracle19c;

    public override Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        // TODO: 接入 Oracle.ManagedDataAccess.Core，校验 v$version banner 包含 19c/21c
        return base.TestConnectionAsync(connectionString);
    }

    public override Task<DryRunResult> DryRunAsync(string sql, string standbyConnectionString, UserRole userRole)
    {
        // 备库连接串必须含 STANDBY 或 READONLY 标识，否则拒绝
        if (!standbyConnectionString.Contains("STANDBY", StringComparison.OrdinalIgnoreCase)
            && !standbyConnectionString.Contains("READONLY", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new DryRunResult
            {
                Success = false,
                ErrorMessage = "【安全拦截】Oracle 预演必须连接 ADG 只读备库"
            });
        }
        return base.DryRunAsync(sql, standbyConnectionString, userRole);
    }
}
