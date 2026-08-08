import { useContext } from 'react';
import { AuthContext } from '../context/AuthContextInstance';
import type { AuthContextValue } from '../types/auth';

/**
 * Access the auth context. Throws (loudly, at development time) if used
 * outside an AuthProvider, rather than silently returning a fake
 * "logged out" value that could hide a missing provider in the tree.
 *
 * Lives here rather than inside context/AuthContext.tsx so that file only
 * exports components (AuthProvider) - a file mixing component and
 * non-component exports breaks Vite Fast Refresh for every consumer of
 * the non-component export, which is exactly what this project's own
 * eslint-plugin-react-refresh config flags.
 */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }

  return context;
}
