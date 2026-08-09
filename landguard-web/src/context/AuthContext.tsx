import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { registerUnauthorizedHandler } from '../api/axios';
import * as authService from '../services/authService';
import { clearSession, loadSession, saveSession } from '../utils/authStorage';
import { isTokenExpired } from '../utils/jwt';
import { AuthContext } from './AuthContextInstance';
import type { AuthContextValue, AuthUser, LoginRequest, UserRole } from '../types/auth';

/**
 * The single owner of the app's live authentication state. Every other
 * auth-aware file (ProtectedRoute, LoginPage, the dashboards,
 * LogoutButton) reads and mutates auth state exclusively through the
 * useAuth() hook (hooks/useAuth.ts) - none of them call authService or
 * authStorage directly - so there is exactly one place that decides what
 * "the user is logged in" actually means at any given moment.
 *
 * The context object itself lives in AuthContextInstance.ts, not here -
 * this file exports only the AuthProvider component, which is what keeps
 * Vite Fast Refresh working for every file that imports it
 * (react-refresh/only-export-components).
 */

interface AuthProviderProps {
  children: ReactNode;
}

interface StartupSession {
  user: AuthUser | null;
  accessToken: string | null;
}

/**
 * Reads and validates whatever session is in storage, synchronously.
 * Used as useState's lazy initializer below rather than an effect, so
 * hydration happens once, during the component's first render, with no
 * extra render cascade and no window where a stale "logged out" state is
 * visible before flipping to "logged in" a tick later.
 */
function readStartupSession(): StartupSession {
  const stored = loadSession();

  if (stored && !isTokenExpired(stored.accessToken)) {
    return { user: stored.user, accessToken: stored.accessToken };
  }

  if (stored) {
    // Present but expired/invalid - clear it outright rather than leaving
    // a dead session in storage for the next visit to fail on again.
    clearSession();
  }

  return { user: null, accessToken: null };
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [{ user, accessToken }, setSession] = useState<StartupSession>(readStartupSession);

  // Because readStartupSession runs synchronously as the lazy initializer
  // above, there is no actual asynchronous startup step left to wait for
  // in this app - isInitializing is always false from the very first
  // render. It is kept as real state (rather than a hardcoded `false`
  // constant) and preserved on AuthContextValue on purpose: it is the
  // seam ProtectedRoute/LoginPage/AppRoutes already use to avoid
  // rendering protected content before auth state is known, and it costs
  // nothing to keep that seam ready for the day startup needs to become
  // genuinely async (e.g. a "verify this token with the backend" call).
  const [isInitializing] = useState(false);

  // Lets axios's response interceptor force an immediate in-memory logout
  // the moment the backend rejects an already-attached token as
  // unauthorized (expired/revoked/account deactivated mid-session) -
  // without axios.ts importing this file or React at all. This is a
  // legitimate effect (subscribing this component to an external system,
  // via a plain callback registration) rather than a synchronous
  // setState-in-effect-body, which is why it's still an effect while
  // startup hydration above no longer is. See api/axios.ts's
  // registerUnauthorizedHandler doc comment.
  useEffect(() => {
    registerUnauthorizedHandler(() => {
      setSession({ user: null, accessToken: null });
    });
  }, []);

  const login = useCallback(async (credentials: LoginRequest): Promise<AuthUser> => {
    const response = await authService.login(credentials);

    saveSession({
      accessToken: response.accessToken,
      user: response.user,
      expiresAtUtc: response.expiresAtUtc,
    });

    setSession({ user: response.user, accessToken: response.accessToken });

    return response.user;
  }, []);

  const logout = useCallback(() => {
    authService.logout();
    setSession({ user: null, accessToken: null });
  }, []);

  const hasRole = useCallback(
    (...roles: UserRole[]) => user !== null && roles.includes(user.role),
    [user],
  );

  const isRole = useCallback((role: UserRole) => user?.role === role, [user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      accessToken,
      isInitializing,
      isAuthenticated: user !== null && accessToken !== null,
      login,
      logout,
      hasRole,
      isRole,
    }),
    [user, accessToken, isInitializing, login, logout, hasRole, isRole],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
