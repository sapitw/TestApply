using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Plugins.Common.Rollback;

namespace CXMTCode.Tests;

public class RollbackGeneratorTests
{
    [Fact]
    public void Insert_should_generate_delete_minus_clause()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql = "INSERT INTO ORDERS(ID, NAME) VALUES(1, 'a')",
            TargetTable = "ORDERS",
            BackupTable = "ORDERS_BAK_X",
            DatabaseType = DatabaseType.Oracle19c,
            PrimaryKeyColumns = new[] { "ID" }
        });
        r.Success.Should().BeTrue();
        r.RollbackSql.Should().Contain("DELETE FROM ORDERS");
        r.RollbackSql.Should().Contain("MINUS");
    }

    [Fact]
    public void Insert_on_mssql_should_use_except()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql = "INSERT INTO ORDERS(ID) VALUES(1)",
            TargetTable = "ORDERS",
            BackupTable = "ORDERS_BAK",
            DatabaseType = DatabaseType.MsSql2019,
            PrimaryKeyColumns = new[] { "ID" }
        });
        r.RollbackSql.Should().Contain("EXCEPT");
    }

    [Fact]
    public void Update_should_generate_set_from_backup()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql = "UPDATE ORDERS SET STATUS='X' WHERE ID=1",
            TargetTable = "ORDERS",
            BackupTable = "ORDERS_BAK",
            DatabaseType = DatabaseType.Oracle19c,
            PrimaryKeyColumns = new[] { "ID" },
            AllColumns = new[] { "ID", "STATUS", "AMOUNT" }
        });
        r.Success.Should().BeTrue();
        r.RollbackSql.Should().Contain("UPDATE ORDERS");
        r.RollbackSql.Should().Contain("SET");
        r.RollbackSql.Should().Contain("STATUS = b.STATUS");
        r.RollbackSql.Should().Contain("AMOUNT = b.AMOUNT");
        r.RollbackSql.Should().Contain("FROM ORDERS_BAK b");
        r.RollbackSql.Should().Contain("WHERE t.ID = b.ID");
    }

    [Fact]
    public void Delete_should_generate_insert_select_from_backup()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql = "DELETE FROM ORDERS WHERE STATUS='X'",
            TargetTable = "ORDERS",
            BackupTable = "ORDERS_BAK",
            DatabaseType = DatabaseType.MySql80,
            AllColumns = new[] { "ID", "NAME", "STATUS" }
        });
        r.Success.Should().BeTrue();
        r.RollbackSql.Should().Contain("INSERT INTO ORDERS");
        r.RollbackSql.Should().Contain("FROM ORDERS_BAK");
    }

    [Fact]
    public void Missing_backup_should_fail()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql  = "DELETE FROM ORDERS WHERE ID=1",
            TargetTable  = "ORDERS",
            BackupTable  = "",
            DatabaseType = DatabaseType.Oracle19c
        });
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("备份表");
    }

    [Fact]
    public void Unsupported_op_should_fail()
    {
        var r = RollbackSqlGenerator.Generate(new GenerationContext
        {
            OriginalSql  = "SELECT * FROM ORDERS",
            TargetTable  = "ORDERS",
            BackupTable  = "ORDERS_BAK",
            DatabaseType = DatabaseType.Oracle19c
        });
        r.Success.Should().BeFalse();
    }
}
