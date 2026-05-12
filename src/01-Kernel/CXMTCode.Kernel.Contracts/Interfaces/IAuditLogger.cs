using CXMTCode.Kernel.Contracts.Enums;
using CXMTCode.Kernel.Contracts.Models;

namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>审计日志接口 - Append Only，SM3 哈希链防篡改</summary>
public interface IAuditLogger
{
    Task<AuditLogWriteResult> WriteAsync(
        Guid userId,
        string userName,
        UserRole userRole,
        string operationType,
        string operationContent,
        string? targetDb = null,
        string? targetTable = null,
        string? sqlStatement = null,
        string? beforeData = null,
        string? afterData = null,
        string? clientIp = null,
        string? result = null,
        SystemEnvironment? environment = null);

    Task<bool> VerifyIntegrityAsync(long logId);

    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        Guid? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? operationType = null,
        int pageIndex = 0,
        int pageSize = 50);
}
