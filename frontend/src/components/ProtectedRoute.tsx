import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { Sprout } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import type { UserRole, VerificationStatus } from '../types/auth';

interface ProtectedRouteProps {
  allowedRoles?: UserRole[];
  requireVerified?: boolean;
  children: React.ReactNode;
}

export function getDefaultRouteForUser(
  role: UserRole,
  verificationStatus?: VerificationStatus
): string {
  if (
    (role === 'Farmer' || role === 'Buyer') &&
    (verificationStatus === 'Unverified' || verificationStatus === 'Pending')
  ) {
    return '/pending';
  }

  switch (role) {
    case 'Buyer':
      return '/buyer/dashboard';
    case 'Agent':
      return '/agent/dashboard';
    case 'Admin':
      return '/admin/dashboard';
    case 'Farmer':
    default:
      return '/farmer/dashboard';
  }
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  allowedRoles,
  requireVerified = false,
  children,
}) => {
  const location = useLocation();
  const { user, token, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-slate-50 gap-3">
        <div className="w-10 h-10 rounded-xl bg-agri-50 border border-emerald-200 flex items-center justify-center text-agri-700 animate-pulse">
          <Sprout className="w-5 h-5" />
        </div>
        <p className="text-xs font-medium text-slate-500">Loading your AgriFlow workspace...</p>
      </div>
    );
  }

  // 1. Not authenticated -> redirect to login, preserving intended destination
  if (!token || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // 2. Role mismatch -> redirect to the user's own default workspace
  if (allowedRoles && !allowedRoles.includes(user.role)) {
    const fallback = getDefaultRouteForUser(user.role, user.verificationStatus);
    return <Navigate to={fallback} replace />;
  }

  // 3. Optional strict verification check (e.g. for placing orders or publishing listings)
  if (requireVerified && user.verificationStatus !== 'Verified') {
    return <Navigate to="/pending" replace />;
  }

  return <>{children}</>;
};

/**
 * Prevents already-authenticated users from seeing /login or /register
 */
export const PublicOnlyRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { user, token, isLoading } = useAuth();

  if (isLoading) {
    return null;
  }

  if (token && user) {
    const target = getDefaultRouteForUser(user.role, user.verificationStatus);
    return <Navigate to={target} replace />;
  }

  return <>{children}</>;
};