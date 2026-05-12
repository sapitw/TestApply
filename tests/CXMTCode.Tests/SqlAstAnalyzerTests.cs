using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Security;

namespace CXMTCode.Tests;

public class SqlAstAnalyzerTests
{
    [Fact]
    public void Strips_comments_and_quoted_strings()
    {
        var sql = "DELETE /* big */ FROM T WHERE NAME='/* fake */' AND TS=NOW() -- trailer";
        var stripped = SqlAstAnalyzer.StripCommentsAndStrings(sql, out var literals);
        stripped.Should().Contain("DELETE");
        stripped.Should().Contain("WHERE");
        stripped.Should().NotContain("/* big */");
        stripped.Should().NotContain("/* fake */");
        stripped.Should().NotContain("trailer");
        literals.Should().Be(1);
    }

    [Fact]
    public void Detects_update_operation_and_set_columns()
    {
        var info = SqlAstAnalyzer.Analyze("UPDATE ORDERS SET STATUS='X', AMOUNT=AMOUNT-1 WHERE ID=10");
        info.OperationType.Should().Be(SqlOperationType.Update);
        info.PrimaryTable.Should().Be("ORDERS");
        info.HasWhereClause.Should().BeTrue();
        info.SetColumns.Should().BeEquivalentTo(new[] { "STATUS", "AMOUNT" });
    }

    [Fact]
    public void Detects_insert_columns_and_values_groups()
    {
        var info = SqlAstAnalyzer.Analyze("INSERT INTO USERS (ID, NAME, EMAIL) VALUES (1,'a','x'),(2,'b','y'),(3,'c','z')");
        info.OperationType.Should().Be(SqlOperationType.Insert);
        info.PrimaryTable.Should().Be("USERS");
        info.InsertColumns.Should().BeEquivalentTo(new[] { "ID", "NAME", "EMAIL" });
        info.InsertValuesGroups.Should().Be(3);
    }

    [Fact]
    public void Detects_join_and_subquery()
    {
        var info = SqlAstAnalyzer.Analyze(
            "SELECT * FROM A JOIN B ON A.ID=B.ID WHERE A.X IN (SELECT X FROM C)");
        info.HasJoin.Should().BeTrue();
        info.HasSubquery.Should().BeTrue();
        info.AllTables.Should().Contain(new[] { "A", "B", "C" });
    }

    [Fact]
    public void Detects_union()
    {
        var info = SqlAstAnalyzer.Analyze("SELECT * FROM A WHERE 1=1 UNION SELECT * FROM B WHERE 1=1");
        info.HasUnion.Should().BeTrue();
    }

    [Fact]
    public void Detects_ddl()
    {
        var info = SqlAstAnalyzer.Analyze("CREATE TABLE T(ID INT)");
        info.OperationType.Should().Be(SqlOperationType.Ddl);
    }

    [Fact]
    public void Detects_delete_from()
    {
        var info = SqlAstAnalyzer.Analyze("DELETE FROM ORDERS WHERE STATUS='CANCELLED'");
        info.OperationType.Should().Be(SqlOperationType.Delete);
        info.PrimaryTable.Should().Be("ORDERS");
        info.HasWhereClause.Should().BeTrue();
    }
}
