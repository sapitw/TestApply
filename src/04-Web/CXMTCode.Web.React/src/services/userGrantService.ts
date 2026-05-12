import request from './request';
import type { ApiResponse } from '../types/common';

export interface UserGrantDto {
  permissionId: string;
  accessId: string;
  userId: string;
  allowSelect: boolean;
  allowInsert: boolean;
  allowUpdate: boolean;
  allowDelete: boolean;
  grantedBy?: string;
  grantedAt: string;
  status: number;
}

export const userGrantService = {
  listByAccess: (accessId: string): Promise<ApiResponse<UserGrantDto[]>> =>
    request.get('/dba/user-grants', { params: { accessId } }),
  grant: (data: Partial<UserGrantDto>): Promise<ApiResponse<UserGrantDto>> =>
    request.post('/dba/user-grants', data),
  update: (id: string, data: Partial<UserGrantDto>): Promise<ApiResponse<boolean>> =>
    request.put(`/dba/user-grants/${id}`, data),
  revoke: (id: string): Promise<ApiResponse<boolean>> =>
    request.delete(`/dba/user-grants/${id}`),
};
