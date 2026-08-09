import type { AuthUser } from '../types/auth';

/**
 * All authentication persistence for the app goes through this file - the
 * only place that knows the actual localStorage key strings or does
 * JSON.parse/stringify on stored auth data. Everything else (AuthContext,
 * axios's interceptor, ProtectedRoute) calls these functions instead of
 * touching `window.localStorage` directly, so there is exactly one place
 * to change if the storage mechanism or key names ever need to change.
 *
 * Deliberately stores only accessToken, user and expiresAtUtc - never the
 * password, which the app never has after the login POST resolves anyway.
 */

const STORAGE_KEYS = {
  accessToken: 'landguard.auth.accessToken',
  user: 'landguard.auth.user',
  expiresAtUtc: 'landguard.auth.expiresAtUtc',
} as const;

export interface StoredSession {
  accessToken: string;
  user: AuthUser;
  expiresAtUtc: string;
}

/**
 * localStorage can throw (private browsing in some browsers, storage
 * disabled by policy, quota exceeded) - every read/write below is wrapped
 * so a storage failure degrades to "no persisted session" instead of
 * crashing the app.
 */
function safeGetItem(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSetItem(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Swallowed on purpose - a failed persist shouldn't fail the login
    // that triggered it; the user is still authenticated for this tab,
    // they just won't have a session restored after a refresh.
  }
}

function safeRemoveItem(key: string): void {
  try {
    window.localStorage.removeItem(key);
  } catch {
    // Swallowed on purpose, same reasoning as safeSetItem.
  }
}

/** Narrow, structural runtime check - guards loadSession() against a corrupted or stale-shape value in localStorage (e.g. left over from an older version of this app) rather than trusting `JSON.parse` output blindly. */
function isAuthUser(value: unknown): value is AuthUser {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate.userId === 'number' &&
    typeof candidate.name === 'string' &&
    typeof candidate.email === 'string' &&
    (candidate.role === 'Buyer' || candidate.role === 'Seller' || candidate.role === 'Admin') &&
    typeof candidate.isActive === 'boolean'
  );
}

/** Persists a freshly-logged-in session. Called once, right after POST /api/auth/login succeeds. */
export function saveSession(session: StoredSession): void {
  safeSetItem(STORAGE_KEYS.accessToken, session.accessToken);
  safeSetItem(STORAGE_KEYS.expiresAtUtc, session.expiresAtUtc);
  safeSetItem(STORAGE_KEYS.user, JSON.stringify(session.user));
}

/**
 * Reads back a previously-saved session. Returns null whenever anything is
 * missing, unparsable, or structurally wrong - AuthContext treats a null
 * result as "no valid session", never a thrown error, on every app-start
 * read.
 */
export function loadSession(): StoredSession | null {
  const accessToken = safeGetItem(STORAGE_KEYS.accessToken);
  const expiresAtUtc = safeGetItem(STORAGE_KEYS.expiresAtUtc);
  const rawUser = safeGetItem(STORAGE_KEYS.user);

  if (!accessToken || !expiresAtUtc || !rawUser) {
    return null;
  }

  try {
    const parsedUser: unknown = JSON.parse(rawUser);

    if (!isAuthUser(parsedUser)) {
      return null;
    }

    return { accessToken, expiresAtUtc, user: parsedUser };
  } catch {
    return null;
  }
}

/** Lightweight accessor for the token alone - used by the Axios request interceptor, which runs on every request and has no need to parse the full user object each time. */
export function getStoredAccessToken(): string | null {
  return safeGetItem(STORAGE_KEYS.accessToken);
}

/** Clears the whole persisted session. Called on logout, and on startup if the stored session turns out to be invalid or expired. */
export function clearSession(): void {
  safeRemoveItem(STORAGE_KEYS.accessToken);
  safeRemoveItem(STORAGE_KEYS.expiresAtUtc);
  safeRemoveItem(STORAGE_KEYS.user);
}
