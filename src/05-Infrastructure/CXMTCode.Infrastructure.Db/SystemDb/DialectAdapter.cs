namespace CXMTCode.Infrastructure.Db.SystemDb;

/// <summary>
/// 跨方言适配。Oracle ↔ SQLite 的常见差异处理：
///   - SYSTIMESTAMP / SYSDATE → CURRENT_TIMESTAMP
///   - NUMBER(n)            → INTEGER
///   - VARCHAR2(n)          → TEXT
///   - CLOB                 → TEXT
/// 仅做开发期占位转换，复杂查询请按方言写两版。
/// </summary>
public sealed class DialectAdapter
{
    public string Provider { get; }
    public DialectAdapter(string provider) => Provider = provider;

    public bool IsOracle => string.Equals(Provider, "Oracle", StringComparison.OrdinalIgnoreCase);
    public bool IsSqlite => string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);

    public string Now => IsOracle ? "SYSTIMESTAMP" : "CURRENT_TIMESTAMP";
}
