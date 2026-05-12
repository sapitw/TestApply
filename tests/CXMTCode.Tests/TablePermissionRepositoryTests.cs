using CXMTCode.Modules.Schema.Repositories;

namespace CXMTCode.Tests;

public class TablePermissionRepositoryTests
{
    private static async Task<TablePermissionRepository> NewAsync()
    {
        var ctx = await TestDbHelper.NewSqliteContextAsync();
        return new TablePermissionRepository(ctx);
    }

    [Fact]
    public async Task Grant_and_list()
    {
        var repo = await NewAsync();
        var accessId = "ACC1";
        var userId   = "U1";
        await repo.GrantAsync(new TablePermissionRecord
        {
            AccessId    = accessId,
            UserId      = userId,
            AllowSelect = true,
            AllowUpdate = true,
            GrantedBy   = "dba"
        });
        var rows = await repo.ListByAccessAsync(accessId);
        rows.Should().HaveCount(1);
        rows[0].AllowSelect.Should().BeTrue();
        rows[0].AllowDelete.Should().BeFalse();
    }

    [Fact]
    public async Task Update_changes_flags()
    {
        var repo = await NewAsync();
        var record = new TablePermissionRecord
        {
            AccessId = "ACC1", UserId = "U1", AllowSelect = true
        };
        await repo.GrantAsync(record);
        record.AllowInsert = true;
        record.AllowSelect = false;
        await repo.UpdateAsync(record);
        var rows = await repo.ListByAccessAsync("ACC1");
        rows[0].AllowInsert.Should().BeTrue();
        rows[0].AllowSelect.Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_sets_status_zero()
    {
        var repo = await NewAsync();
        var record = new TablePermissionRecord { AccessId = "A", UserId = "U", AllowSelect = true };
        await repo.GrantAsync(record);
        await repo.RevokeAsync(record.PermissionId);
        var active = await repo.ListByUserAsync("U");
        active.Should().BeEmpty();
    }
}
