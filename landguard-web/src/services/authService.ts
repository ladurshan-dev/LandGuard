import { apiClient } from '../api/axios';
import { clearSession } from '../utils/authStorage';
import { ApiError, toApiError } from '../utils/apiError';
import type { AuthUser, LoginRequest, LoginResponse, UserRole } from '../types/auth';

/**
 * The data-access layer for authentication - HTTP calls and the safety
 * checks around them, nothing else. This file never imports React, never
 * touches component state, and (aside from delegating logout's storage
 * cleanup to authStorage) never decides what the *app* should do with the
 * result - that is AuthContext's job. Keeping this boundary means
 * AuthContext and LoginPage can both call `login()` and get identical,
 * already-safe behaviour instead of re-implementing error handling twice.
 *
 * Error normalization is shared with every other service via
 * utils/apiError.ts - this file only supplies the one piece of wording
 * that is genuinely login-specific ("Invalid email or password." instead
 * of apiError's generic "You are not authenticated."), rather than
 * re-implementing AxiosError/response-shape handling itself.
 */

const KNOWN_ROLES: readonly UserRole[] = ['Buyer', 'Seller', 'Admin'];

function isAuthUserShape(value: unknown): value is AuthUser {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate.userId === 'number' &&
    typeof candidate.name === 'string' &&
    typeof candidate.email === 'string' &&
    typeof candidate.role === 'string' &&
    typeof candidate.isActive === 'boolean'
  );
}

/** Guards against a malformed/unexpected 200 - the "malformed response" case called out in the Authentication Foundation's error-handling list. */
function isLoginResponseShape(value: unknown): value is LoginResponse {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate.accessToken === 'string' &&
    typeof candidate.expiresAtUtc === 'string' &&
    isAuthUserShape(candidate.user)
  );
}

/**
 * Calls POST /auth/login and returns the response exactly as the backend
 * sent it, after two checks the backend's own contract can't enforce from
 * this side of the wire:
 *  - the response actually has the shape LoginResponse promises (the
 *    "malformed response" case);
 *  - `user.role` is one of the three roles this app knows how to route -
 *    an unrecognized role is treated as a hard failure, never a silent
 *    login.
 * Deliberately never touches localStorage - persisting a session is
 * AuthContext's decision, not this function's.
 */
export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  try {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials);

    if (!isLoginResponseShape(response.data)) {
      // Not an HTTP error - the backend answered 200 with a body that
      // doesn't match its own contract - so `status` is null here rather
      // than the (successful) HTTP status code.
      throw new ApiError('Unexpected response from the server. Please try again.', null, [], false);
    }

    if (!KNOWN_ROLES.includes(response.data.user.role)) {
      throw new ApiError(
        'Your account role is not recognized. Please contact an administrator.',
        null,
        [],
        false,
      );
    }

    return response.data;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    // Login's one piece of endpoint-specific wording: a 401 here always
    // means "wrong email or password" (see AuthController.Login), which
    // reads better than apiError's generic 401 default - every other
    // status falls through to that shared default unchanged.
    throw toApiError(error, { statusMessages: { 401: 'Invalid email or password.' } });
  }
}

/**
 * Clears the persisted session. This backend has no server-side token
 * revocation/blacklist endpoint - logout is purely a client-side JWT
 * discard - so this is the entire logout operation at the data layer.
 * AuthContext calls this and then resets its own in-memory state.
 *
 * No separate getCurrentUser()/session-read helper is added here on
 * purpose: authStorage.loadSession() already does exactly that, and
 * AuthContext (the only caller) can use it directly - wrapping it a
 * second time here would just be an extra layer with nothing to add.
 */
export function logout(): void {
  clearSession();
}
