import request from './request';
import type { ApiResponse, SystemEnvironment } from '../types/common';
import type { DbConnectionDto, DbConnectionUpsertDto } from '../types/dbConnection';

export const dbConnectionService = {
  list: (env?: SystemEnvironment): Promise<ApiResponse<DbConnectionDto[]>> =>
    request.get('/dba/db-connections', { params: env ? { env } : undefined }),

  get: (id: string): Promise<ApiResponse<DbConnectionDto>> =>
    request.get(`/dba/db-connections/${id}`),

  create: (data: DbConnectionUpsertDto): Promise<ApiResponse<DbConnectionDto>> =>
    request.post('/dba/db-connections', data),

  update: (id: string, data: DbConnectionUpsertDto): Promise<ApiResponse<DbConnectionDto>> =>
    request.put(`/dba/db-connections/${id}`, data),

  remove: (id: string): Promise<ApiResponse<boolean>> =>
    request.delete(`/dba/db-connections/${id}`),

  test: (id: string): Promise<ApiResponse<{ success: boolean; databaseVersion?: string; errorMessage?: string }>> =>
    request.post(`/dba/db-connections/${id}/test`),

  activate: (id: string): Promise<ApiResponse<boolean>> =>
    request.post(`/dba/db-connections/${id}/activate`),

  currentEnv: (): Promise<ApiResponse<SystemEnvironment>> =>
    request.get('/environment/current'),

  switchEnv: (target: SystemEnvironment, reason: string): Promise<ApiResponse<SystemEnvironment>> =>
    request.post('/admin/environment/switch', { targetEnvironment: target, reason }),
};
