import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { UserRole, hasRole as hasRoleHelper } from '../types/user';

interface Props {
  required?: UserRole;
  children: React.ReactNode;
}

export const RequireAuth: React.FC<Props> = ({ required, children }) => {
  const { isAuthenticated, user } = useAuthStore();
  const location = useLocation();
  if (!isAuthenticated) {
    return <Navigate to={`/login?redirect=${encodeURIComponent(location.pathname)}`} replace />;
  }
  if (required && !hasRoleHelper(user?.role, required)) {
    return <Navigate to="/forbidden" replace />;
  }
  return <>{children}</>;
};

export default RequireAuth;
