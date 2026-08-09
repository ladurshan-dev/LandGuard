import type { ReactElement } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { FullScreenLoader } from './FullScreenLoader';
import { useAuth } from '../hooks/useAuth';
import type { UserRole } from '../types/auth';

interface ProtectedRouteProps {
  children: ReactElement;
  /** When omitted, any authenticated user (any role) may access the route. When given, only a user whose role is in this list may. */
  allowedRoles?: UserRole[];
}

/**
 * Route guard. Three states, checked in order, so protected content is
 * never rendered before we actually know it's allowed to be:
 *
 * 1. Still initializing (AuthContext hasn't finished its first
 *    localStorage read yet) - render a loading indicator, nothing else.
 *    This is what prevents both a flash of dashboard content for a user
 *    who turns out to be logged out, and a flash-redirect-to-/login for a
 *    user who turns out to still have a valid session.
 * 2. Not authenticated - redirect to /login, remembering where they were
 *    headed (`state.from`) so a future "return to where you were" flow
 *    has somewhere to read that from.
 * 3. Authenticated but role-restricted and not permitted - redirect to
 *    `/`, which itself resolves to that user's correct dashboard (see
 *    AppRoutes), rather than a dead end or a raw "access denied" page.
 */
export function ProtectedRoute({ children, allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isInitializing, hasRole } = useAuth();
  const location = useLocation();

  if (isInitializing) {
    return <FullScreenLoader label="Checking your session" />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && !hasRole(...allowedRoles)) {
    return <Navigate to="/" replace />;
  }

  return children;
}
