import request from './request';
import type { ApiResponse } from '../types/common';
import type { LoginPayload, LoginRequest, UserInfo } from '../types/user';
import type { MenuConfig } from '../types/common';

export const authService = {
  login: (req: LoginRequest): Promise<ApiResponse<LoginPayload>> =>
    request.post('/auth/login', req),

  profile: (): Promise<ApiResponse<UserInfo>> => request.get('/me/profile'),

  menus: (): Promise<ApiResponse<MenuConfig[]>> => request.get('/me/menus'),

  routes: (): Promise<ApiResponse<string[]>> => request.get('/me/routes'),

  permissions: (): Promise<ApiResponse<string[]>> => request.get('/me/permissions'),
};
