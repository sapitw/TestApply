import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { jwtDecode } from 'jwt-decode';
import { hasRole, UserRole, type LoginPayload, type UserInfo } from '../types/user';

interface AuthState {
  user: UserInfo | null;
  token: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  login: (r: LoginPayload) => void;
  logout: () => void;
  hasRole: (r: UserRole) => boolean;
  isDbaOrAbove: () => boolean;
  isSysAdmin: () => boolean;
}

interface JwtPayload {
  nameid?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'?: string;
  unique_name?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'?: string;
  given_name?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'?: string;
  role?: string;
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string;
  dept_code?: string;
  dept_name?: string;
}

const NAMEID_KEY = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const NAME_KEY   = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const GN_KEY     = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname';
const ROLE_KEY   = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function parseUser(payload: LoginPayload): UserInfo {
  const claims = jwtDecode<JwtPayload>(payload.token);
  return {
    userId: claims[NAMEID_KEY] || claims.nameid || payload.user.userId,
    userName: claims[NAME_KEY] || claims.unique_name || payload.user.userName,
    displayName: claims[GN_KEY] || claims.given_name || payload.user.displayName,
    role: ((claims[ROLE_KEY] || claims.role) as UserRole) || payload.user.role,
    departmentCode: claims.dept_code || payload.user.departmentCode,
    departmentName: claims.dept_name || payload.user.departmentName,
    email: payload.user.email,
    isActive: payload.user.isActive,
  };
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      refreshToken: null,
      isAuthenticated: false,

      login: (payload) => {
        const user = parseUser(payload);
        set({
          token: payload.token,
          refreshToken: payload.refreshToken,
          user,
          isAuthenticated: true,
        });
      },

      logout: () => {
        set({ user: null, token: null, refreshToken: null, isAuthenticated: false });
      },

      hasRole: (required) => hasRole(get().user?.role, required),
      isDbaOrAbove: () => hasRole(get().user?.role, UserRole.DBA),
      isSysAdmin: () => get().user?.role === UserRole.SysAdmin,
    }),
    {
      name: 'cxmtcode-auth',
      partialize: (s) => ({
        token: s.token,
        refreshToken: s.refreshToken,
        user: s.user,
        isAuthenticated: s.isAuthenticated,
      }),
    }
  )
);
