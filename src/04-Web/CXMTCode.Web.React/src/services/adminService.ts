import request from './request';
import type { ApiResponse } from '../types/common';
import type { UserInfo, UserRole } from '../types/user';

export interface CreateUserDto {
  userName: string;
  password: string;
  displayName: string;
  role: UserRole;
  departmentCode?: string;
  departmentName?: string;
  email?: string;
}

export const adminService = {
  listUsers: (): Promise<ApiResponse<UserInfo[]>> => request.get('/admin/users'),
  createUser: (dto: CreateUserDto): Promise<ApiResponse<UserInfo>> =>
    request.post('/admin/users', dto),
  updateRole: (id: string, role: UserRole): Promise<ApiResponse<boolean>> =>
    request.put(`/admin/users/${id}/role`, { role }),
  toggleActive: (id: string, active: boolean): Promise<ApiResponse<boolean>> =>
    request.put(`/admin/users/${id}/active`, { active }),
  listConfig: (): Promise<ApiResponse<Record<string, string>>> => request.get('/admin/config'),
  updateConfig: (key: string, value: string): Promise<ApiResponse<boolean>> =>
    request.put(`/admin/config/${key}`, { value }),
};
