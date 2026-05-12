using System.Text;
using System.Text.RegularExpressions;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Adapters;

namespace CXMTCode.Plugins.Common.Rollback;

/// <summary>
/// 回滚 SQL 生成器 - 基于「变更前备份表 + 主键」反向生成 DML。
/// <para>
/// 支持三种正向操作的反向生成：
///   - INSERT → DELETE（用 SELECT 备份表得到的主键值）
///   - UPDATE → UPDATE（用备份表的 OLD 行恢复 SET 字段）
///   - DELETE → INSERT（把备份表所有行整行 INSERT 回原表）
/// </para>
/// <para>
/// 设计假设：执行阶段在 D3 备份插件中按 <c>原表名_BAK_yyyyMMddHHmmss</c>
/// 整表备份了即将被修改的行（DELETE 场景全表备份，UPDATE 场景按 WHERE 条件备份）。
/// 主键列由调用方通过 <see cref="GenerationContext.PrimaryKeyColumns"/> 提供，
/// 没有主键时回退到全列等值比较。
/// </para>
/// </summary>
public static class RollbackSqlGenerator
{
    public static RollbackSqlResult Generate(GenerationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.OriginalSql))
            return Fail("原始 SQL 为空，无法生成回滚");
        if (string.IsNullOrWhiteSpace(ctx.BackupTable))
            return Fail("缺少备份表名，无法生成回滚");

        var op = DetectOperation(ctx.OriginalSql);
        return op switch
        {
            SqlOperationType.Insert => GenerateForInsert(ctx),
            SqlOperationType.Update => GenerateForUpdate(ctx),
            SqlOperationType.Delete => GenerateForDelete(ctx),
            _ => Fail($"不支持的操作类型 {op}，仅支持 INSERT/UPDATE/DELETE")
        };
    }

    // INSERT 操作：根据主键从备份表外的「新插入行」反向 DELETE
    // 备份表存的是变更前的行集，因此「变更后表 MINUS 备份表」 = 新插入行
    private static RollbackSqlResult GenerateForInsert(GenerationContext ctx)
    {
        var keys = ResolveKeys(ctx);
        var keyCols = string.Join(", ", keys);
        var sql = $@"-- 回滚 INSERT：删除变更后新增的行
DELETE FROM {ctx.TargetTable}
WHERE ({keyCols}) IN (
    SELECT {keyCols} FROM {ctx.TargetTable}
    {GenerateMinusClause(ctx, keys)}
);";
        return Ok(sql);
    }

    // UPDATE 操作：用备份表的 OLD 值按主键 UPDATE 回去
    private static RollbackSqlResult GenerateForUpdate(GenerationContext ctx)
    {
        var keys    = ResolveKeys(ctx);
        var columns = ctx.AllColumns?.Count > 0
            ? ctx.AllColumns.Where(c => !keys.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList()
            : new List<string>();

        if (columns.Count == 0)
            return Fail("UPDATE 回滚需要 AllColumns 列出非主键列");

        var setClause = string.Join(",\n    ",
            columns.Select(c => $"{c} = b.{c}"));
        var joinClause = string.Join(" AND ",
            keys.Select(k => $"t.{k} = b.{k}"));

        var sql = $@"-- 回滚 UPDATE：把备份表 {ctx.BackupTable} 的 OLD 值恢复到 {ctx.TargetTable}
UPDATE {ctx.TargetTable} t
SET
    {setClause}
FROM {ctx.BackupTable} b
WHERE {joinClause};";
        return Ok(sql);
    }

    // DELETE 操作：把备份表内容整体插回原表
    private static RollbackSqlResult GenerateForDelete(GenerationContext ctx)
    {
        var columns = ctx.AllColumns?.Count > 0
            ? "(" + string.Join(", ", ctx.AllColumns) + ")"
            : string.Empty;
        var sql = $@"-- 回滚 DELETE：从备份表 {ctx.BackupTable} 整体恢复到 {ctx.TargetTable}
INSERT INTO {ctx.TargetTable}{columns}
SELECT {(string.IsNullOrEmpty(columns) ? "*" : string.Join(", ", ctx.AllColumns))} FROM {ctx.BackupTable};";
        return Ok(sql);
    }

    private static IReadOnlyList<string> ResolveKeys(GenerationContext ctx)
    {
        if (ctx.PrimaryKeyColumns?.Count > 0) return ctx.PrimaryKeyColumns;
        if (ctx.AllColumns?.Count > 0)        return ctx.AllColumns;        // 回退到全列等值
        return new[] { "ID" };                                              // 最弱保底
    }

    private static string GenerateMinusClause(GenerationContext ctx, IReadOnlyList<string> keys)
    {
        var keyCols = string.Join(", ", keys);
        // Oracle / DB2 用 MINUS，PG/MySQL/MSSQL 用 EXCEPT
        var op = ctx.DatabaseType switch
        {
            DatabaseType.Oracle19c or DatabaseType.Db2_115 => "MINUS",
            _ => "EXCEPT"
        };
        return $"{op} SELECT {keyCols} FROM {ctx.BackupTable}";
    }

    private static SqlOperationType DetectOperation(string sql)
    {
        var trimmed = sql.TrimStart().ToUpperInvariant();
        if (trimmed.StartsWith("INSERT")) return SqlOperationType.Insert;
        if (trimmed.StartsWith("UPDATE")) return SqlOperationType.Update;
        if (trimmed.StartsWith("DELETE")) return SqlOperationType.Delete;
        if (Regex.IsMatch(trimmed, @"^(CREATE|ALTER|DROP|TRUNCATE)\s")) return SqlOperationType.Ddl;
        return SqlOperationType.Unknown;
    }

    private static RollbackSqlResult Ok(string sql) =>
        new() { Success = true, RollbackSql = sql };
    private static RollbackSqlResult Fail(string msg) =>
        new() { Success = false, ErrorMessage = msg };
}

/// <summary>回滚生成上下文 - 由 D3 备份 + 元数据查询填充</summary>
public sealed class GenerationContext
{
    public required string OriginalSql  { get; init; }
    public required string TargetTable  { get; init; }
    public required string BackupTable  { get; init; }
    public required DatabaseType DatabaseType { get; init; }
    public IReadOnlyList<string>? PrimaryKeyColumns { get; init; }
    public IReadOnlyList<string>? AllColumns        { get; init; }
}
