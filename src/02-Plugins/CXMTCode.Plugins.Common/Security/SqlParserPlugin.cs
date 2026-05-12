using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// B1 SQL 语法解析插件 - 格式化、去注释、去不可见字符、识别基本操作类型。
/// <para>
/// 注：完整 ANTLR4 AST 解析待 D 系列回滚生成器一并接入。当前实现以正则+字符流为主，
/// 提供 99% 场景下需要的 OpType / 表名提取 / 是否带 WHERE 等信息。
/// </para>
/// </summary>
public class SqlParserPlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.SqlParser";
    public override string DisplayName => "B1 SQL 语法解析";
    public override string Version => "3.0.0";

    private readonly SqlFormatter _formatter = new();

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var sql = input.GetParameter<string>("sql") ?? string.Empty;
        // 先做基本格式化（去注释 / 去不可见字符），再用增强的 AST 分析器抽取语义信息
        var formatted = _formatter.Format(sql);
        var cleaned   = _formatter.RemoveComments(formatted);
        cleaned       = _formatter.RemoveInvisibleChars(cleaned);

        var ast = SqlAstAnalyzer.Analyze(cleaned);

        return Task.FromResult(PluginOutput.Ok(new ParsedSqlResult
        {
            OriginalSql      = sql,
            CleanedSql       = cleaned,
            OperationType    = ast.OperationType,
            TableNames       = ast.AllTables.Count > 0 ? ast.AllTables : new List<string> { ast.PrimaryTable }.Where(s => !string.IsNullOrEmpty(s)).ToList(),
            HasWhereClause   = ast.HasWhereClause,
            PrimaryTable     = ast.PrimaryTable,
            SetColumns       = ast.SetColumns,
            InsertColumns    = ast.InsertColumns,
            HasJoin          = ast.HasJoin,
            HasUnion         = ast.HasUnion,
            HasSubquery      = ast.HasSubquery,
        }));
    }

    private static SqlOperationType DetectOpType(string sql)
    {
        var u = sql.TrimStart().ToUpperInvariant();
        if (u.StartsWith("SELECT")) return SqlOperationType.Select;
        if (u.StartsWith("INSERT")) return SqlOperationType.Insert;
        if (u.StartsWith("UPDATE")) return SqlOperationType.Update;
        if (u.StartsWith("DELETE")) return SqlOperationType.Delete;
        if (u.StartsWith("CREATE") || u.StartsWith("ALTER") || u.StartsWith("DROP") || u.StartsWith("TRUNCATE"))
            return SqlOperationType.Ddl;
        return SqlOperationType.Unknown;
    }

    private static List<string> ExtractTableNames(string sql, SqlOperationType op)
    {
        var u = sql.ToUpperInvariant();
        var result = new List<string>();
        string? marker = op switch
        {
            SqlOperationType.Update => "UPDATE",
            SqlOperationType.Delete => "FROM",
            SqlOperationType.Insert => "INTO",
            SqlOperationType.Select => "FROM",
            _ => null
        };
        if (marker is null) return result;

        var idx = u.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return result;
        var rest = sql[(idx + marker.Length)..].TrimStart();
        var endIdx = rest.IndexOfAny(new[] { ' ', '\n', '\r', '\t', '(', ';' });
        var name = endIdx > 0 ? rest[..endIdx] : rest;
        if (!string.IsNullOrEmpty(name)) result.Add(name.TrimEnd(';'));
        return result;
    }

    private static bool ContainsKeyword(string sql, string keyword)
    {
        var idx = 0; var u = sql.ToUpperInvariant();
        while ((idx = u.IndexOf(keyword, idx, StringComparison.Ordinal)) >= 0)
        {
            bool l = idx == 0 || !char.IsLetterOrDigit(u[idx - 1]);
            bool r = idx + keyword.Length == u.Length || !char.IsLetterOrDigit(u[idx + keyword.Length]);
            if (l && r) return true;
            idx += keyword.Length;
        }
        return false;
    }
}

public class ParsedSqlResult
{
    public string OriginalSql { get; set; } = "";
    public string CleanedSql { get; set; } = "";
    public SqlOperationType OperationType { get; set; }
    public List<string> TableNames { get; set; } = new();
    public bool HasWhereClause { get; set; }
    public string PrimaryTable { get; set; } = "";
    public List<string> SetColumns { get; set; } = new();
    public List<string> InsertColumns { get; set; } = new();
    public bool HasJoin { get; set; }
    public bool HasUnion { get; set; }
    public bool HasSubquery { get; set; }
}

public sealed class SqlFormatter
{
    public string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
        return string.Join(" ", sql.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0));
    }

    public string RemoveComments(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return string.Empty;
        // /* ... */ 多行注释
        while (sql.Contains("/*"))
        {
            var s = sql.IndexOf("/*", StringComparison.Ordinal);
            var e = sql.IndexOf("*/", s, StringComparison.Ordinal);
            if (e < 0) break;
            sql = sql.Remove(s, e - s + 2);
        }
        // -- 单行注释
        return string.Join("\n", sql.Split('\n').Select(l =>
        {
            var i = l.IndexOf("--", StringComparison.Ordinal);
            return i >= 0 ? l[..i].TrimEnd() : l;
        }));
    }

    public string RemoveInvisibleChars(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var c in sql)
        {
            if (c is ' ' or '\t' or '\n' or '\r') sb.Append(c);
            else if (!char.IsControl(c) && c != '​' && c != '﻿') sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
