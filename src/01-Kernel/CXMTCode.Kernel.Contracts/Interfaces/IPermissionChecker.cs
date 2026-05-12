using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>权限校验中枢 - 硬编码安全红线，不可修改 / 不可关闭 / 不可绕过</summary>
public interface IPermissionChecker
{
    Task<PermissionCheckResult> CheckRoleAsync(IUserContext user, UserRole requiredRole);

    Task<PermissionCheckResult> CheckTablePermissionAsync(
        IUserContext user, string dbName, string tableName, SqlOperationType opType);

    Task<PermissionCheckResult> CheckSqlExecutionPermissionAsync(
        IUserContext user, string sql, DatabaseType dbType);

    Task<PermissionCheckResult> CheckOwnershipAsync(IUserContext user, Guid dataOwnerId);
}
