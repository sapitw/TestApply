using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.DB2_115;

/// <summary>
/// C4 IBM Db2 11.5~12.1.4 适配。
/// <para>
/// 由于 IBM.Data.Db2 / Net.IBM.Data.Db2 需要 Linux 原生 clidriver 与许可证文件（IBM_DB_HOME），
/// 在通用 CI / 容器环境中不便直接打包。建议在生产 Docker 镜像中按以下方式接入：
/// </para>
/// <code>
/// FROM mcr.microsoft.com/dotnet/aspnet:8.0
/// RUN apt-get update &amp;&amp; apt-get install -y libxml2 libstdc++6 ...
/// ENV IBM_DB_HOME=/opt/ibm/db2/clidriver
/// COPY clidriver /opt/ibm/db2/clidriver
/// </code>
/// 当前实现继承 <see cref="AdapterStubBase"/>，所有方法返回明确的「未接入驱动」错误，
/// 保证微内核 / 业务层路径在没有 DB2 时仍可跑通；HADR STANDBY 校验逻辑保留。
/// </summary>
public sealed class Db2Adapter : AdapterStubBase
{
    public override DatabaseType DatabaseType => DatabaseType.Db2_115;

    public override Task<DryRunResult> DryRunAsync(string sql, string standbyConn, UserRole userRole)
    {
        // 即便驱动未接入，仍校验连接串必须明确指向 HADR STANDBY
        var c = (standbyConn ?? string.Empty).ToUpperInvariant();
        if (!c.Contains("HADR") && !c.Contains("STANDBY") && !c.Contains("READONLY"))
        {
            return Task.FromResult(new DryRunResult
            {
                Success = false,
                ErrorMessage = "【安全拦截】Db2 预演必须连接 HADR STANDBY"
            });
        }
        return base.DryRunAsync(sql, standbyConn, userRole);
    }

    public override Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
        => Task.FromResult(new ConnectionTestResult
        {
            Success = false,
            ErrorMessage = "[Db2 驱动未接入] 请在生产镜像中安装 IBM clidriver 并替换 AdapterStubBase 实现",
            DatabaseVersion = DatabaseType.GetVersionName()
        });
}
