import type { ReactElement } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { FullScreenLoader } from './FullScreenLoader';
import { useAuth } from '../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../types/auth';
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
 *    that user's OWN dashboard via DASHBOARD_PATH_BY_ROLE (e.g. a Buyer
 *    hitting "/seller/properties" lands on "/buyer/dashboard"), not to
 *    "/". Security is unaffected either way - the protected content below
 *    this guard is never rendered for a role mismatch, full stop - this is
 *    purely about where they land afterward.
 *
 *    Historical note (public homepage change): "/" used to be a
 *    RootRedirect component that immediately sent any authenticated user
 *    to their dashboard, so redirecting a role-mismatched user to "/" and
 *    redirecting them to their dashboard were the same thing. "/" is now
 *    the actual public HomePage for every visitor (see AppRoutes.tsx) and
 *    no longer redirects anyone anywhere. Redirecting a role mismatch to
 *    "/" today would land a signed-in user on the public marketing page
 *    instead of their own dashboard - a UX regression, not a security one -
 *    so this now reads DASHBOARD_PATH_BY_ROLE directly instead, preserving
 *    the original "send them back to where they belong" intent. Falls back
 *    to "/" only in the practically-unreachable case where `user` is
 *    somehow null despite `isAuthenticated` being true.
 */
export function ProtectedRoute({ children, allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isInitializing, hasRole, user } = useAuth();
  const location = useLocation();

  if (isInitializing) {
    return <FullScreenLoader label="Checking your session" />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && !hasRole(...allowedRoles)) {
    return <Navigate to={user ? DASHBOARD_PATH_BY_ROLE[user.role] : '/'} replace />;
  }

  return children;
}
