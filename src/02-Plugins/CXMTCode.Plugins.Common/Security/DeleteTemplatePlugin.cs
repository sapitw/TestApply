using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Kernel.PluginLoading;

namespace CXMTCode.Plugins.Common.Security;

/// <summary>
/// B4 DELETE 模板匹配。
/// 规则：普通用户 DELETE 必须与某条 Active 模板 100% 结构一致，仅参数值可变。
/// 实现：把模板与待执行 SQL 同时规整化（去空白、关键字大写、值占位化），逐 token 比对。
/// </summary>
public class DeleteTemplatePlugin : PluginBase
{
    public override string PluginId => "CXMTCode.Plugins.Common.DeleteTemplate";
    public override string DisplayName => "B4 DELETE 模板匹配";
    public override string Version => "3.0.0";

    public override Task<PluginOutput> ExecuteAsync(PluginInput input)
    {
        var sql        = input.GetParameter<string>("sql") ?? "";
        var table      = input.GetParameter<string>("tableName") ?? "";
        var templates  = input.GetParameter<List<DeleteTemplate>>("templates") ?? new();
        var userRole   = input.GetParameter<UserRole>("userRole");

        var upper = sql.TrimStart().ToUpperInvariant();
        if (!upper.StartsWith("DELETE"))
            return Task.FromResult(PluginOutput.Ok(new TemplateMatchResult
            { IsMatched = false, MatchType = TemplateMatchType.NotApplicable }));

        if (userRole >= UserRole.DBA)
            return Task.FromResult(PluginOutput.Ok(new TemplateMatchResult
            {
                IsMatched = true,
                MatchType = TemplateMatchType.AdminBypass,
                MatchedTemplateId = "ADMIN_BYPASS"
            }));

        var actives = templates.Where(t =>
            string.Equals(t.TableName, table, StringComparison.OrdinalIgnoreCase) &&
            t.Status == TemplateStatus.Active).ToList();

        if (actives.Count == 0)
            return Task.FromResult(PluginOutput.Ok(new TemplateMatchResult
            {
                IsMatched = false,
                Message = $"表 {table} 无激活的 DELETE 模板",
                MatchType = TemplateMatchType.NoTemplate,
                RiskLevel = RiskLevel.Critical
            }));

        var sqlNorm = StructuralNormalize(sql);
        foreach (var t in actives)
        {
            var tmplNorm = StructuralNormalize(t.TemplateSql);
            if (sqlNorm == tmplNorm)
                return Task.FromResult(PluginOutput.Ok(new TemplateMatchResult
                {
                    IsMatched = true,
                    MatchedTemplateId = t.TemplateId,
                    TemplateName = t.Name,
                    MatchType = TemplateMatchType.StructureMatch
                }));
        }

        return Task.FromResult(PluginOutput.Ok(new TemplateMatchResult
        {
            IsMatched = false,
            Message = "与所有模板均不匹配",
            MatchType = TemplateMatchType.NoMatch,
            RiskLevel = RiskLevel.Critical
        }));
    }

    /// <summary>结构化归一化：参数字面量替换为占位符 ?，便于模板比对</summary>
    private static string StructuralNormalize(string sql)
    {
        var s = (sql ?? string.Empty).Trim().ToUpperInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, "'[^']*'", "?");
        s = System.Text.RegularExpressions.Regex.Replace(s, "\\b\\d+(\\.\\d+)?\\b", "?");
        s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ");
        return s;
    }
}

public enum TemplateStatus { Draft = 0, PendingApproval = 1, Active = 2, Disabled = 3 }
public enum TemplateMatchType { NotApplicable, StructureMatch, NoTemplate, NoMatch, AdminBypass }

public class DeleteTemplate
{
    public string TemplateId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string TableName { get; set; } = "";
    public string TemplateSql { get; set; } = "";
    public List<TemplateParameter>? Parameters { get; set; }
    public int MaxAffectedRows { get; set; } = 1000;
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
}

public class TemplateParameter
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "STRING";
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public int? MaxLength { get; set; }
}

public class TemplateMatchResult
{
    public bool IsMatched { get; set; }
    public string MatchedTemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string Message { get; set; } = "";
    public TemplateMatchType MatchType { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
}
