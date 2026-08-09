import { AxiosError } from 'axios';

/**
 * Framework-independent (no React/React Router/MUI - just `axios`, this
 * layer's own HTTP client) backend-error normalization, shared by every
 * service's data-access layer (authService today; propertyService and
 * whatever follows next). Every service throws one `ApiError` from its
 * catch block instead of letting a raw AxiosError escape, so every caller
 * - a form, a page, a toast - only ever needs to catch one type and read
 * `.message` for a safe, user-facing string, or `.errors` for the full
 * list when a caller wants to render field-by-field validation messages.
 *
 * The extraction logic below is not guessed - it is read directly from
 * the two real response shapes this backend actually returns, confirmed
 * by inspecting the controllers and ExceptionHandlingMiddleware
 * themselves:
 *
 * 1. Every controller action's `Result.Failure` path returns
 *    `{ errors: string[] }` - every controller in this API
 *    (Auth/Property/Fraud/Ocr/DocumentComparison) does
 *    `BadRequest(new { errors = result.Errors })` /
 *    `Unauthorized(...)` / `NotFound(...)` / `StatusCode(403, ...)` with
 *    exactly this shape.
 * 2. Anything that instead reaches ExceptionHandlingMiddleware (a thrown
 *    exception, not a Result.Failure) returns
 *    `{ status, title, errors, traceId }` - and critically, `errors` is
 *    populated only for a FluentValidation failure. Every other mapped
 *    exception (NotFoundException -> 404, DomainException -> 422,
 *    UnauthorizedAccessException -> 403, SqlException -> 400, or a
 *    genuine unhandled exception -> 500) leaves `errors` EMPTY and puts
 *    its one human-readable message in `title` instead. Reading only
 *    `errors[0]` (as this project's original auth-only extraction did)
 *    silently loses the message for every one of those cases - Property
 *    and Fraud will hit NotFoundException/DomainException paths far more
 *    than Auth ever does, so this is a real gap, not a hypothetical one.
 */

/** Framework-independent, throwable, catchable with `instanceof ApiError`. */
export class ApiError extends Error {
  /** HTTP status code, or null when the request never reached the server at all (network failure, not a server error). */
  readonly status: number | null;
  /** Every message the backend actually supplied, in order - `.message` is always `errors[0]` when there is at least one. Most callers only need `.message`; a future validation-heavy form can render the full list. */
  readonly errors: string[];
  /** True only when the request never reached the server (offline, DNS failure, backend not running, CORS rejection) - as opposed to the server responding with an error status. */
  readonly isNetworkError: boolean;

  constructor(message: string, status: number | null, errors: string[], isNetworkError: boolean) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
    this.isNetworkError = isNetworkError;
  }
}

/**
 * Deliberately generic, non-module-specific default text per status - e.g.
 * NOT "Invalid email or password.", which is login-specific wording that
 * belongs in authService (via the `statusMessages` option below), not
 * here. This file has no idea what operation failed.
 */
const DEFAULT_MESSAGE_BY_STATUS: Readonly<Record<number, string>> = {
  400: 'The request could not be processed. Please check your input and try again.',
  401: 'You are not authenticated. Please sign in and try again.',
  403: 'You do not have permission to do that.',
  404: 'The requested resource could not be found.',
  409: 'This could not be completed because it conflicts with the current data. Please refresh and try again.',
};

const DEFAULT_SERVER_ERROR_MESSAGE = 'An unexpected server error occurred. Please try again later.';
const DEFAULT_NETWORK_ERROR_MESSAGE =
  'Unable to reach the LandGuard server. Please check your connection and try again.';
const DEFAULT_UNKNOWN_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/** Reads every message the backend actually supplied, trying the two real response shapes above in priority order. Never invents a field that isn't in either shape. */
function extractBackendMessages(data: unknown): string[] {
  if (typeof data !== 'object' || data === null) {
    return [];
  }

  const candidate = data as Record<string, unknown>;

  if (Array.isArray(candidate.errors) && candidate.errors.length > 0) {
    const messages = candidate.errors.filter((entry): entry is string => typeof entry === 'string');

    if (messages.length > 0) {
      return messages;
    }
  }

  // Middleware shape's `title` - the real message for every mapped
  // exception type except FluentValidation's (which populates `errors`
  // above instead).
  if (typeof candidate.title === 'string' && candidate.title.length > 0) {
    return [candidate.title];
  }

  return [];
}

function defaultMessageForStatus(status: number): string {
  if (status in DEFAULT_MESSAGE_BY_STATUS) {
    return DEFAULT_MESSAGE_BY_STATUS[status];
  }

  return status >= 500 ? DEFAULT_SERVER_ERROR_MESSAGE : DEFAULT_UNKNOWN_ERROR_MESSAGE;
}

export interface ToApiErrorOptions {
  /** Overrides the generic default message for specific status codes - only used when the backend itself supplied no message. E.g. authService passes `{ 401: 'Invalid email or password.' }` for login specifically, instead of this file's generic "You are not authenticated." */
  statusMessages?: Partial<Record<number, string>>;
  /** Overrides the generic network-error message (no response reached the server at all). */
  networkErrorMessage?: string;
  /** Overrides the generic fallback used when `error` isn't even an AxiosError. */
  unknownErrorMessage?: string;
}

/**
 * Normalizes anything a service's API call can throw into one ApiError.
 * This is the one function every module's service layer should call from
 * its catch block - see authService.login for the reference usage, and
 * reuse the same pattern for propertyService/fraudService/etc.
 */
export function toApiError(error: unknown, options: ToApiErrorOptions = {}): ApiError {
  if (error instanceof AxiosError) {
    if (!error.response) {
      // No response at all - the request never reached the server
      // (backend not running, DNS/connection failure, CORS rejection).
      return new ApiError(options.networkErrorMessage ?? DEFAULT_NETWORK_ERROR_MESSAGE, null, [], true);
    }

    const status = error.response.status;
    const backendMessages = extractBackendMessages(error.response.data);
    const message = backendMessages[0] ?? options.statusMessages?.[status] ?? defaultMessageForStatus(status);

    return new ApiError(message, status, backendMessages, false);
  }

  return new ApiError(options.unknownErrorMessage ?? DEFAULT_UNKNOWN_ERROR_MESSAGE, null, [], false);
}
