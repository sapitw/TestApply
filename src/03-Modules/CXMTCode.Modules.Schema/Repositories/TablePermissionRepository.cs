using Dapper;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;

namespace CXMTCode.Modules.Schema.Repositories;

/// <summary>
/// 用户表级授权仓储 - 操作 CXMT_TABLE_PERMISSIONS。
/// </summary>
public sealed class TablePermissionRepository : OracleRepositoryBase
{
    public TablePermissionRepository(ISystemDbContext db) : base(db) { }

    public async Task<IReadOnlyList<TablePermissionRecord>> ListByAccessAsync(string accessId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT PERMISSION_ID, ACCESS_ID, USER_ID,
                   ALLOW_SELECT, ALLOW_INSERT, ALLOW_UPDATE, ALLOW_DELETE,
                   GRANTED_BY, GRANTED_AT, STATUS
            FROM CXMT_TABLE_PERMISSIONS
            WHERE ACCESS_ID=@aid ORDER BY GRANTED_AT DESC", new { aid = accessId });
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TablePermissionRecord>> ListByUserAsync(string userId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT PERMISSION_ID, ACCESS_ID, USER_ID,
                   ALLOW_SELECT, ALLOW_INSERT, ALLOW_UPDATE, ALLOW_DELETE,
                   GRANTED_BY, GRANTED_AT, STATUS
            FROM CXMT_TABLE_PERMISSIONS
            WHERE USER_ID=@uid AND STATUS=1 ORDER BY GRANTED_AT DESC", new { uid = userId });
        return rows.Select(Map).ToList();
    }

    public async Task<int> GrantAsync(TablePermissionRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_TABLE_PERMISSIONS
                (PERMISSION_ID, ACCESS_ID, USER_ID,
                 ALLOW_SELECT, ALLOW_INSERT, ALLOW_UPDATE, ALLOW_DELETE,
                 GRANTED_BY, STATUS)
            VALUES(@PermissionId, @AccessId, @UserId,
                   @AllowSelect, @AllowInsert, @AllowUpdate, @AllowDelete,
                   @GrantedBy, 1)",
            new {
                r.PermissionId, r.AccessId, r.UserId,
                AllowSelect = r.AllowSelect ? 1 : 0,
                AllowInsert = r.AllowInsert ? 1 : 0,
                AllowUpdate = r.AllowUpdate ? 1 : 0,
                AllowDelete = r.AllowDelete ? 1 : 0,
                r.GrantedBy
            });
    }

    public async Task<int> UpdateAsync(TablePermissionRecord r)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_TABLE_PERMISSIONS
            SET ALLOW_SELECT=@s, ALLOW_INSERT=@i, ALLOW_UPDATE=@u, ALLOW_DELETE=@d
            WHERE PERMISSION_ID=@PermissionId",
            new {
                r.PermissionId,
                s = r.AllowSelect ? 1 : 0,
                i = r.AllowInsert ? 1 : 0,
                u = r.AllowUpdate ? 1 : 0,
                d = r.AllowDelete ? 1 : 0
            });
    }

    public async Task<int> RevokeAsync(string permissionId)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE CXMT_TABLE_PERMISSIONS SET STATUS=0 WHERE PERMISSION_ID=@id",
            new { id = permissionId });
    }

    private static TablePermissionRecord Map(dynamic r) => new()
    {
        PermissionId = (string)r.PERMISSION_ID,
        AccessId     = (string)r.ACCESS_ID,
        UserId       = (string)r.USER_ID,
        AllowSelect  = Convert.ToInt32(r.ALLOW_SELECT) == 1,
        AllowInsert  = Convert.ToInt32(r.ALLOW_INSERT) == 1,
        AllowUpdate  = Convert.ToInt32(r.ALLOW_UPDATE) == 1,
        AllowDelete  = Convert.ToInt32(r.ALLOW_DELETE) == 1,
        GrantedBy    = (string?)r.GRANTED_BY,
        GrantedAt    = DateTime.TryParse((string?)r.GRANTED_AT, out DateTime d) ? d : DateTime.UtcNow,
        Status       = Convert.ToInt32(r.STATUS)
    };
}

public class TablePermissionRecord
{
    public string PermissionId { get; set; } = Guid.NewGuid().ToString("N");
    public string AccessId { get; set; } = "";
    public string UserId { get; set; } = "";
    public bool AllowSelect { get; set; }
    public bool AllowInsert { get; set; }
    public bool AllowUpdate { get; set; }
    public bool AllowDelete { get; set; }
    public string? GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public int Status { get; set; } = 1;
}
