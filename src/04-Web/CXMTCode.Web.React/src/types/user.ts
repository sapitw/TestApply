export enum UserRole {
  User = 'User',
  DBA = 'DBA',
  SysAdmin = 'SysAdmin',
}

export const UserRoleRank: Record<UserRole, number> = {
  [UserRole.User]: 1,
  [UserRole.DBA]: 2,
  [UserRole.SysAdmin]: 3,
};

export const UserRoleLabel: Record<UserRole, string> = {
  [UserRole.User]: '普通用户',
  [UserRole.DBA]: 'DBA 管理员',
  [UserRole.SysAdmin]: '系统管理员',
};

export const UserRoleColor: Record<UserRole, string> = {
  [UserRole.User]: 'blue',
  [UserRole.DBA]: 'orange',
  [UserRole.SysAdmin]: 'red',
};

export function hasRole(current: UserRole | undefined, required: UserRole): boolean {
  if (!current) return false;
  return UserRoleRank[current] >= UserRoleRank[required];
}

export interface UserInfo {
  userId: string;
  userName: string;
  displayName: string;
  role: UserRole;
  departmentCode?: string;
  departmentName?: string;
  email?: string;
  isActive: boolean;
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginPayload {
  token: string;
  refreshToken: string;
  expiresIn: number;
  user: UserInfo;
}
