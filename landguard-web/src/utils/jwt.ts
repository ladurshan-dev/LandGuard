import { jwtDecode } from 'jwt-decode';

/**
 * The only part of a JWT this app ever reads. The user's identity and role
 * always come from the backend's login response body (see AuthUser in
 * types/auth.ts) - never from decoding the token - so the token stays an
 * opaque bearer credential as far as any authorization decision goes.
 * jwt-decode is used here purely to read `exp`, so AuthContext can tell a
 * merely-present stored token apart from an actually-still-valid one on
 * startup.
 */
interface DecodedTokenClaims {
  exp?: number;
}

/**
 * True if the token has no `exp` claim, can't be decoded at all (malformed
 * or tampered), or has already expired. Treating "can't tell" the same as
 * "expired" is the safe default for a check that gates restoring a
 * session.
 */
export function isTokenExpired(token: string): boolean {
  try {
    const claims = jwtDecode<DecodedTokenClaims>(token);

    if (typeof claims.exp !== 'number') {
      return true;
    }

    return claims.exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}
