namespace CXMTCode.Kernel.Contracts.Enums;

/// <summary>支持的业务数据库类型（含版本信息）</summary>
public enum DatabaseType
{
    Oracle19c = 1,
    MsSql2019 = 2,
    MySql80 = 3,
    Db2_115 = 4
}

public static class DatabaseTypeExtensions
{
    public static string GetVersionName(this DatabaseType dbType) => dbType switch
    {
        DatabaseType.Oracle19c => "Oracle Database 19c Enterprise Edition",
        DatabaseType.MsSql2019 => "Microsoft SQL Server 2019 Enterprise",
        DatabaseType.MySql80 => "MySQL 8.0+",
        DatabaseType.Db2_115 => "IBM Db2 11.5~12.1.4 Enterprise",
        _ => "Unknown"
    };
}
