import request from './request';
import type { ApiResponse } from '../types/common';

export interface AuditLogEntry {
  logId: number;
  logTime: string;
  userId: string;
  userName: string;
  userRole: number;
  environmentType?: string;
  operationType: string;
  operationDesc: string;
  targetDatabase?: string;
  targetTable?: string;
  sqlStatement?: string;
  clientIp?: string;
  result: string;
  hashValue: string;
  prevHash?: string;
}

export interface AuditQuery {
  userId?: string;
  from?: string;
  to?: string;
  operationType?: string;
  pageIndex?: number;
  pageSize?: number;
}

export const auditService = {
  own: (params?: AuditQuery): Promise<ApiResponse<AuditLogEntry[]>> =>
    request.get('/audit/own', { params }),
  all: (params?: AuditQuery): Promise<ApiResponse<AuditLogEntry[]>> =>
    request.get('/audit/all', { params }),
  verify: (logId: number): Promise<ApiResponse<boolean>> =>
    request.get(`/audit/verify/${logId}`),
};
