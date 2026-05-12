using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;
using CXMTCode.Plugins.Common.Security;
using FluentAssertions;

namespace CXMTCode.Tests;

public class PluginTests
{
    [Fact]
    public async Task SqlParser_detects_operation_and_table()
    {
        var p = new SqlParserPlugin();
        var r = await p.ExecuteAsync(new PluginInput().Set("sql", "DELETE FROM ORDERS WHERE STATUS='X'"));
        var data = r.Data as ParsedSqlResult;
        data!.OperationType.Should().Be(SqlOperationType.Delete);
        data.HasWhereClause.Should().BeTrue();
        data.TableNames.Should().Contain("ORDERS");
    }

    [Fact]
    public async Task AstValidator_blocks_update_without_where()
    {
        var p = new AstValidatorPlugin();
        var r = await p.ExecuteAsync(new PluginInput()
            .Set("sql", "UPDATE T SET A=1")
            .Set("userRole", UserRole.User));
        var data = r.Data as AstValidationResult;
        data!.IsValid.Should().BeFalse();
        data.Violations.Should().Contain(v => v.Contains("RL003"));
    }

    [Fact]
    public async Task DeleteTemplate_admin_bypass()
    {
        var p = new DeleteTemplatePlugin();
        var r = await p.ExecuteAsync(new PluginInput()
            .Set("sql", "DELETE FROM T WHERE ID=1")
            .Set("tableName", "T")
            .Set("templates", new List<DeleteTemplate>())
            .Set("userRole", UserRole.DBA));
        var data = r.Data as TemplateMatchResult;
        data!.MatchType.Should().Be(TemplateMatchType.AdminBypass);
    }

    [Fact]
    public async Task DeleteTemplate_user_must_match_template()
    {
        var p = new DeleteTemplatePlugin();
        var template = new DeleteTemplate
        {
            Name = "DEL_ORDERS_BY_STATUS",
            TableName = "ORDERS",
            TemplateSql = "DELETE FROM ORDERS WHERE STATUS='X'",
            Status = TemplateStatus.Active
        };
        var r = await p.ExecuteAsync(new PluginInput()
            .Set("sql", "DELETE FROM ORDERS WHERE STATUS='SUCCESS'")
            .Set("tableName", "ORDERS")
            .Set("templates", new List<DeleteTemplate> { template })
            .Set("userRole", UserRole.User));
        var data = r.Data as TemplateMatchResult;
        data!.IsMatched.Should().BeTrue();
        data.MatchType.Should().Be(TemplateMatchType.StructureMatch);
    }
}
