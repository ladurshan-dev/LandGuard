/**
 * Types for the Authentication module. Every shape here is a direct,
 * field-for-field mirror of what POST /api/auth/login actually returns
 * (see the backend's AuthResponse/UserProfile DTOs) - nothing here is
 * invented or "nice to have". Keeping these in their own file (rather than
 * inline in authService/AuthContext) means every layer that touches auth
 * data - the API client, the context, ProtectedRoute, the dashboards -
 * imports the same single source of truth instead of redeclaring shapes
 * that could quietly drift apart.
 */

/** Matches dbo.Users.Role / UserRoleExtensions.ToDbValue() exactly - "Admin", not "Administrator". */
export type UserRole = 'Buyer' | 'Seller' | 'Admin';

/** The `user` object nested in POST /api/auth/login's response body. */
export interface AuthUser {
  userId: number;
  name: string;
  email: string;
  role: UserRole;
  nic: string | null;
  phone: string | null;
  nicVerified: boolean;
  isActive: boolean;
  createdAt: string;
}

/** POST /api/auth/login's request body. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** POST /api/auth/login's response body, exactly as given. */
export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}

/** What useAuth()/AuthContext exposes to the rest of the app. */
export interface AuthContextValue {
  /** The logged-in user, or null when signed out. */
  user: AuthUser | null;
  /** The raw JWT, or null when signed out - rarely needed directly (axios attaches it automatically), but exposed for edge cases (e.g. a websocket handshake). Named to match the backend response field exactly, not a generic "token". */
  accessToken: string | null;
  /** True only until the very first localStorage read on app load resolves - lets ProtectedRoute avoid a false "redirect to /login" flash before we know whether a session exists. */
  isInitializing: boolean;
  /** Derived convenience flag - true whenever both an accessToken and a user are present. */
  isAuthenticated: boolean;
  login: (credentials: LoginRequest) => Promise<AuthUser>;
  logout: () => void;
  /** True if a user is logged in AND their role is one of the given roles - the one place "is this user allowed here" is decided, so ProtectedRoute/pages never compare `user.role === '...'` directly. */
  hasRole: (...roles: UserRole[]) => boolean;
  /** Convenience single-role form of hasRole. */
  isRole: (role: UserRole) => boolean;
}

/** Where each role lands after login - single source of truth reused by LoginPage's redirect and any "go to my dashboard" link. */
export const DASHBOARD_PATH_BY_ROLE: Record<UserRole, string> = {
  Seller: '/seller/dashboard',
  Buyer: '/buyer/dashboard',
  Admin: '/admin/dashboard',
};
