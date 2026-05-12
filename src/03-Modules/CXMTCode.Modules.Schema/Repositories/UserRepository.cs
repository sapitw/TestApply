using Dapper;
using CXMTCode.Infrastructure.Db;
using CXMTCode.Infrastructure.Db.SystemDb;
using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Modules.Schema.Models;

namespace CXMTCode.Modules.Schema.Repositories;

public sealed class UserRepository : OracleRepositoryBase
{
    public UserRepository(ISystemDbContext systemDb) : base(systemDb) { }

    public async Task<UserRecord?> GetByUserNameAsync(string userName)
    {
        using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT USER_ID, USER_NAME, PASSWORD_HASH, PASSWORD_SALT, DISPLAY_NAME, ROLE,
                   DEPARTMENT_CODE, DEPARTMENT_NAME, EMAIL, IS_ACTIVE,
                   LAST_LOGIN_AT, LAST_LOGIN_IP, CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY
            FROM CXMT_USERS WHERE USER_NAME=@n", new { n = userName });
        return Map(row);
    }

    public async Task<UserRecord?> GetByIdAsync(string userId)
    {
        using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT USER_ID, USER_NAME, PASSWORD_HASH, PASSWORD_SALT, DISPLAY_NAME, ROLE,
                   DEPARTMENT_CODE, DEPARTMENT_NAME, EMAIL, IS_ACTIVE,
                   LAST_LOGIN_AT, LAST_LOGIN_IP, CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY
            FROM CXMT_USERS WHERE USER_ID=@id", new { id = userId });
        return Map(row);
    }

    public async Task<IReadOnlyList<UserRecord>> ListAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT USER_ID, USER_NAME, PASSWORD_HASH, PASSWORD_SALT, DISPLAY_NAME, ROLE,
                   DEPARTMENT_CODE, DEPARTMENT_NAME, EMAIL, IS_ACTIVE,
                   LAST_LOGIN_AT, LAST_LOGIN_IP, CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY
            FROM CXMT_USERS ORDER BY CREATED_AT DESC");
        return rows.Select(Map).Where(u => u != null).Cast<UserRecord>().ToList();
    }

    public async Task<int> InsertAsync(UserRecord u)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            INSERT INTO CXMT_USERS(USER_ID, USER_NAME, PASSWORD_HASH, PASSWORD_SALT, DISPLAY_NAME, ROLE,
                DEPARTMENT_CODE, DEPARTMENT_NAME, EMAIL, IS_ACTIVE, CREATED_BY)
            VALUES(@UserId, @UserName, @PasswordHash, @PasswordSalt, @DisplayName, @Role,
                @DepartmentCode, @DepartmentName, @Email, @IsActiveInt, @CreatedBy)",
            new
            {
                u.UserId, u.UserName, u.PasswordHash, u.PasswordSalt, u.DisplayName,
                Role = (int)u.Role, u.DepartmentCode, u.DepartmentName, u.Email,
                IsActiveInt = u.IsActive ? 1 : 0, u.CreatedBy
            });
    }

    public async Task<int> UpdateRoleAsync(string userId, UserRole role, string? updatedBy)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_USERS SET ROLE=@r, UPDATED_AT=CURRENT_TIMESTAMP, UPDATED_BY=@by WHERE USER_ID=@id",
            new { r = (int)role, by = updatedBy, id = userId });
    }

    public async Task<int> UpdateActiveAsync(string userId, bool active, string? updatedBy)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_USERS SET IS_ACTIVE=@a, UPDATED_AT=CURRENT_TIMESTAMP, UPDATED_BY=@by WHERE USER_ID=@id",
            new { a = active ? 1 : 0, by = updatedBy, id = userId });
    }

    public async Task<int> UpdateLastLoginAsync(string userId, string clientIp)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(@"
            UPDATE CXMT_USERS SET LAST_LOGIN_AT=CURRENT_TIMESTAMP, LAST_LOGIN_IP=@ip WHERE USER_ID=@id",
            new { ip = clientIp, id = userId });
    }

    private static UserRecord? Map(dynamic? r)
    {
        if (r is null) return null;
        return new UserRecord
        {
            UserId         = (string)r.USER_ID,
            UserName       = (string)r.USER_NAME,
            PasswordHash   = (string)r.PASSWORD_HASH,
            PasswordSalt   = (string)r.PASSWORD_SALT,
            DisplayName    = (string?)r.DISPLAY_NAME ?? "",
            Role           = (UserRole)Convert.ToInt32(r.ROLE),
            DepartmentCode = (string?)r.DEPARTMENT_CODE,
            DepartmentName = (string?)r.DEPARTMENT_NAME,
            Email          = (string?)r.EMAIL,
            IsActive       = Convert.ToInt32(r.IS_ACTIVE) == 1,
            LastLoginAt    = ParseDate((string?)r.LAST_LOGIN_AT),
            LastLoginIp    = (string?)r.LAST_LOGIN_IP,
            CreatedAt      = ParseDate((string?)r.CREATED_AT) ?? DateTime.UtcNow,
            CreatedBy      = (string?)r.CREATED_BY,
            UpdatedAt      = ParseDate((string?)r.UPDATED_AT),
            UpdatedBy      = (string?)r.UPDATED_BY
        };
    }

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt : (DateTime?)null;
}
