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

/**
 * Seller Government Identity Verification requirement - matches
 * dbo.Users.IdentityStatus's raw string values exactly. Null for a
 * Buyer/Admin (an identity check only ever applies to a Seller) or for a
 * Seller account that predates this requirement and has not yet
 * registered/reverified.
 */
export type IdentityStatus = 'Pending' | 'Verified' | 'Failed' | null;

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
  /** Only meaningful for role === 'Seller' - see IdentityStatus's own doc comment. */
  identityStatus: IdentityStatus;
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

/**
 * POST /api/auth/register's request body - mirrors the backend's
 * RegisterRequest exactly. `role` is a plain field here (not implied by a
 * separate endpoint per role) - the backend re-validates it server-side
 * regardless of what is sent, so this type existing doesn't grant the
 * frontend any authority over it; it only shapes what RegisterPage submits.
 */
export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  /** Buyer or Seller only - Register never offers Admin as a choice. */
  role: Extract<UserRole, 'Buyer' | 'Seller'>;
  /** Required when role is 'Seller'; omitted (or ignored if sent) for 'Buyer'. */
  nic?: string;
}

/** POST /api/auth/register's response body - identical shape to LoginResponse (register logs the new account straight in). */
export type RegisterResponse = LoginResponse;

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
  /** Registers a new Buyer or Seller account and, on success, logs it straight in exactly like login() does (same session persistence, same return shape) - see authService.register's doc comment for why. */
  register: (request: RegisterRequest) => Promise<AuthUser>;
  /** Seller Government Identity Verification requirement - re-runs the identity check for the signed-in Seller and updates the cached user (identityStatus only; the JWT/session itself is untouched, no new token is issued). */
  reverifyIdentity: () => Promise<AuthUser>;
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
