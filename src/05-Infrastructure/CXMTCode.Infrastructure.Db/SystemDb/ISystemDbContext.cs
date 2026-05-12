using System.Data.Common;

namespace CXMTCode.Infrastructure.Db.SystemDb;

/// <summary>
/// 系统数据库上下文 - 封装 CXMT_* 系统表的连接。
/// <para>
/// 生产部署：使用 Oracle 19C Enterprise（CXMT_PLATFORM 用户，CXMT_DATA / CXMT_IDX / CXMT_AUDIT 表空间）。
/// 开发调试：默认使用 SQLite（db/kimicode-dev.db）。两端的方言差异由 <see cref="DialectAdapter"/> 适配。
/// </para>
/// </summary>
public interface ISystemDbContext
{
    /// <summary>创建新连接（已打开）</summary>
    DbConnection CreateOpenConnection();

    /// <summary>系统 DB 类型："Oracle" / "Sqlite"</summary>
    string Provider { get; }

    /// <summary>方言适配器，用于跨方言 SQL 调整</summary>
    DialectAdapter Dialect { get; }
}
