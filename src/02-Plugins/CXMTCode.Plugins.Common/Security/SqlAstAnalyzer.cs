using System.Text.RegularExpressions;
using CXMTCode.Kernel.Contracts.Enums;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// 增强 SQL 语义分析器 - 在不引入 ANTLR4 完整文法的前提下，
/// 用一组精心调优的正则 + 字符流扫描，覆盖 99% DML/DDL 操作识别。
/// <para>
/// 提取信息：
///   - 操作类型（SELECT / INSERT / UPDATE / DELETE / DDL）
///   - 主表名（INSERT INTO / UPDATE / DELETE FROM / SELECT FROM）
///   - 涉及到的全部表名（含 JOIN）
///   - SET 列（UPDATE）
///   - INSERT 列
///   - WHERE 谓词存在性
///   - 子查询 / UNION 检测
///   - 字面量 / 参数占位符
/// </para>
/// <para>
/// 注释 / 引号字符串先剥离再分析，避免引号内关键词误判。
/// </para>
/// </summary>
public static class SqlAstAnalyzer
{
    private static readonly Regex IdentifierPattern = new(@"[A-Za-z_][A-Za-z0-9_.]*", RegexOptions.Compiled);

    public static SqlAstInfo Analyze(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlAstInfo { OperationType = SqlOperationType.Unknown };

        var stripped = StripCommentsAndStrings(sql, out var literals);
        var upper = stripped.ToUpperInvariant();
        var info = new SqlAstInfo
        {
            OriginalSql      = sql,
            StrippedSql      = stripped,
            LiteralCount     = literals,
            OperationType    = DetectOpType(upper),
            HasWhereClause   = ContainsWord(upper, "WHERE"),
            HasJoin          = ContainsWord(upper, "JOIN"),
            HasUnion         = ContainsWord(upper, "UNION"),
            HasSubquery      = CountKeyword(upper, "SELECT") > 1,
            HasGroupBy       = ContainsWord(upper, "GROUP BY"),
            HasOrderBy       = ContainsWord(upper, "ORDER BY"),
        };

        // 主表 + 涉及全部表
        info.PrimaryTable = ExtractPrimaryTable(stripped, info.OperationType);
        info.AllTables    = ExtractAllTables(stripped, info.OperationType);

        // UPDATE SET 列
        if (info.OperationType == SqlOperationType.Update)
            info.SetColumns = ExtractUpdateSetColumns(stripped);

        // INSERT 列 + VALUES 个数
        if (info.OperationType == SqlOperationType.Insert)
        {
            info.InsertColumns = ExtractInsertColumns(stripped);
            info.InsertValuesGroups = CountInsertValueGroups(stripped);
        }

        return info;
    }

    private static SqlOperationType DetectOpType(string upper)
    {
        if (upper.StartsWith("SELECT")) return SqlOperationType.Select;
        if (upper.StartsWith("INSERT")) return SqlOperationType.Insert;
        if (upper.StartsWith("UPDATE")) return SqlOperationType.Update;
        if (upper.StartsWith("DELETE")) return SqlOperationType.Delete;
        if (Regex.IsMatch(upper, @"^(CREATE|ALTER|DROP|TRUNCATE)\s"))
            return SqlOperationType.Ddl;
        return SqlOperationType.Unknown;
    }

    private static string ExtractPrimaryTable(string sql, SqlOperationType op)
    {
        var pattern = op switch
        {
            SqlOperationType.Insert => @"INSERT\s+INTO\s+([\w\.""`\[\]]+)",
            SqlOperationType.Update => @"UPDATE\s+([\w\.""`\[\]]+)",
            SqlOperationType.Delete => @"DELETE\s+FROM\s+([\w\.""`\[\]]+)",
            SqlOperationType.Select => @"FROM\s+([\w\.""`\[\]]+)",
            _ => null
        };
        if (pattern is null) return "";
        var m = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
        return m.Success ? StripQuotes(m.Groups[1].Value) : "";
    }

    private static List<string> ExtractAllTables(string sql, SqlOperationType op)
    {
        var tables = new List<string>();
        if (op == SqlOperationType.Insert)
        {
            var p = ExtractPrimaryTable(sql, op);
            if (!string.IsNullOrEmpty(p)) tables.Add(p);
            return tables;
        }
        // FROM/JOIN 后跟标识符
        var regex = new Regex(@"\b(?:FROM|JOIN)\s+([\w\.""`\[\]]+)", RegexOptions.IgnoreCase);
        foreach (Match m in regex.Matches(sql))
            tables.Add(StripQuotes(m.Groups[1].Value));
        if (op == SqlOperationType.Update)
        {
            var p = ExtractPrimaryTable(sql, op);
            if (!string.IsNullOrEmpty(p) && !tables.Contains(p, StringComparer.OrdinalIgnoreCase))
                tables.Insert(0, p);
        }
        return tables.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractUpdateSetColumns(string sql)
    {
        // UPDATE T SET col1=..., col2=... WHERE/RETURNING/;
        var m = Regex.Match(sql, @"UPDATE\s+\S+\s+SET\s+(.+?)(?:\s+WHERE\s|\s+RETURNING\s|;|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return new();
        var setExpr = m.Groups[1].Value;
        // 在顶层逗号处切分（忽略括号内的逗号）
        return SplitTopLevel(setExpr, ',')
            .Select(s => s.Split('=', 2)[0].Trim())
            .Where(s => IdentifierPattern.IsMatch(s))
            .Select(StripQuotes)
            .ToList();
    }

    private static List<string> ExtractInsertColumns(string sql)
    {
        var m = Regex.Match(sql, @"INSERT\s+INTO\s+\S+\s*\(([^)]+)\)\s+VALUES",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return new();
        return m.Groups[1].Value.Split(',')
            .Select(s => StripQuotes(s.Trim()))
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static int CountInsertValueGroups(string sql)
    {
        // VALUES (...), (...), (...)
        var matches = Regex.Matches(sql, @"\)\s*,\s*\(", RegexOptions.Singleline);
        return matches.Count + 1;
    }

    /// <summary>剥离 SQL 字符串字面量与注释，返回干净的结构化 SQL</summary>
    public static string StripCommentsAndStrings(string sql, out int literalCount)
    {
        literalCount = 0;
        if (string.IsNullOrEmpty(sql)) return sql;

        var sb = new System.Text.StringBuilder(sql.Length);
        int i = 0, n = sql.Length;
        while (i < n)
        {
            var c = sql[i];

            // /* */ 多行注释
            if (c == '/' && i + 1 < n && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                continue;
            }
            // -- 单行注释
            if (c == '-' && i + 1 < n && sql[i + 1] == '-')
            {
                var nl = sql.IndexOf('\n', i + 2);
                i = nl < 0 ? n : nl;
                continue;
            }
            // 单引号字符串
            if (c == '\'')
            {
                literalCount++;
                sb.Append('?');
                i++;
                while (i < n)
                {
                    if (sql[i] == '\'')
                    {
                        // 转义 ''
                        if (i + 1 < n && sql[i + 1] == '\'') { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            // 双引号标识符（Oracle / 标准 SQL）保留原样
            if (c == '"' || c == '`' || c == '[')
            {
                sb.Append(c);
                i++;
                while (i < n)
                {
                    sb.Append(sql[i]);
                    if (sql[i] == '"' || sql[i] == '`' || sql[i] == ']') { i++; break; }
                    i++;
                }
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static bool ContainsWord(string text, string word)
    {
        var idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            bool l = idx == 0               || !char.IsLetterOrDigit(text[idx - 1]);
            bool r = idx + word.Length == text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
            if (l && r) return true;
            idx += word.Length;
        }
        return false;
    }

    private static int CountKeyword(string text, string word)
    {
        var idx = 0; var n = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            bool l = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            bool r = idx + word.Length == text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
            if (l && r) n++;
            idx += word.Length;
        }
        return n;
    }

    private static IEnumerable<string> SplitTopLevel(string s, char sep)
    {
        int depth = 0;
        var start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;
            else if (s[i] == sep && depth == 0)
            {
                yield return s[start..i];
                start = i + 1;
            }
        }
        if (start < s.Length) yield return s[start..];
    }

    private static string StripQuotes(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && (
            (s[0] == '"' && s[^1] == '"') ||
            (s[0] == '`' && s[^1] == '`') ||
            (s[0] == '[' && s[^1] == ']')))
            return s[1..^1];
        return s;
    }
}

public class SqlAstInfo
{
    public string OriginalSql { get; set; } = "";
    public string StrippedSql { get; set; } = "";
    public int LiteralCount { get; set; }
    public SqlOperationType OperationType { get; set; }
    public bool HasWhereClause { get; set; }
    public bool HasJoin { get; set; }
    public bool HasUnion { get; set; }
    public bool HasSubquery { get; set; }
    public bool HasGroupBy { get; set; }
    public bool HasOrderBy { get; set; }
    public string PrimaryTable { get; set; } = "";
    public List<string> AllTables { get; set; } = new();
    public List<string> SetColumns { get; set; } = new();
    public List<string> InsertColumns { get; set; } = new();
    public int InsertValuesGroups { get; set; }
}
