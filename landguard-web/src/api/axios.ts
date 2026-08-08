import axios from 'axios';
import type { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { clearSession, getStoredAccessToken } from '../utils/authStorage';

/**
 * Base URL for every backend call this app makes. Hardcoded rather than
 * read from a Vite env var for now - this is a university-project
 * frontend with exactly one backend target, and the brief was explicit
 * about the exact base URL to use; if a staging/production split is ever
 * needed, this constant is the one place to change or replace with
 * import.meta.env.
 */
const API_BASE_URL = 'http://localhost:5080/api';

/**
 * The backend's own origin (no "/api" suffix), derived from API_BASE_URL
 * rather than a second hardcoded string - this is the one place that
 * constant lives. Needed for resolving the root-relative URLs the backend
 * returns for uploaded property images (e.g.
 * "/uploads/properties/31/xxx.png", via FileStorageSettings.PublicBaseUrl
 * and app.UseStaticFiles() serving wwwroot) - see resolveAssetUrl below
 * for why a plain `<img src={...}>` can't use that value as-is.
 */
const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

/**
 * Resolves a backend-supplied asset URL (property images today) against
 * the API's own origin, not the page the browser happens to be on.
 * PropertyImageSummary.imageUrl/PropertyListingResult.coverImageUrl come
 * back from the backend as root-relative paths
 * (`/uploads/properties/{id}/{file}`) - correct as far as the backend is
 * concerned, since app.UseStaticFiles() serves them from its own origin,
 * http://localhost:5080. But the frontend dev server runs on a different
 * origin (http://localhost:5173), so an <img src="/uploads/..."> resolves
 * against *that* origin instead and hits Vite's own dev server (which
 * falls back to the SPA), not the API - the exact "broken image, URL
 * loads the login page" bug this fixes. Already-absolute URLs (a future
 * CDN/blob-storage backend returning a full https:// URL) are returned
 * unchanged.
 */
export function resolveAssetUrl(path: string): string {
  return /^https?:\/\//i.test(path) ? path : `${API_ORIGIN}${path.startsWith('/') ? path : `/${path}`}`;
}

/**
 * Requests whose own 401 is an expected, user-facing outcome (wrong
 * credentials) rather than "an already-issued token stopped being valid" -
 * the response interceptor below must not treat these as a session expiry,
 * or a failed login attempt would also clear/redirect as if the user had
 * been logged out of a session that never existed. This is also what
 * keeps the interceptor from ever looping: it never reacts to a 401 on an
 * auth endpoint, so there is no path where handling a 401 triggers another
 * request that can itself 401.
 */
const AUTH_ENDPOINTS_EXEMPT_FROM_UNAUTHORIZED_HANDLING = [
  '/auth/login',
  '/auth/register/buyer',
  '/auth/register/seller',
];

type UnauthorizedHandler = () => void;

let unauthorizedHandler: UnauthorizedHandler | null = null;

/**
 * Lets AuthContext hook itself into "the backend just told us this token
 * is no longer valid" without this file importing React or AuthContext -
 * axios.ts stays a plain HTTP client, AuthContext stays the only owner of
 * in-memory auth state. AuthContext calls this once, from its mount
 * effect; ProtectedRoute then reacts to AuthContext's state changing, so
 * this file never has to perform navigation itself.
 */
export function registerUnauthorizedHandler(handler: UnauthorizedHandler): void {
  unauthorizedHandler = handler;
}

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Attaches "Authorization: Bearer <token>" whenever a token is actually
// stored - and, just as importantly, attaches nothing at all when there is
// none, rather than sending "Bearer null"/"Bearer undefined".
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = getStoredAccessToken();

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const requestUrl = error.config?.url ?? '';
    const isExemptAuthEndpoint = AUTH_ENDPOINTS_EXEMPT_FROM_UNAUTHORIZED_HANDLING.some((path) =>
      requestUrl.includes(path),
    );

    if (error.response?.status === 401 && !isExemptAuthEndpoint) {
      // The backend rejected an already-attached token (expired, revoked,
      // or the account was deactivated mid-session). Clear the dead
      // session so no further request goes out carrying it, and notify
      // AuthContext (if it has registered a handler) so the app's
      // in-memory auth state flips immediately and ProtectedRoute
      // redirects on its own next render.
      clearSession();
      unauthorizedHandler?.();
    }

    return Promise.reject(error);
  },
);

export default apiClient;
