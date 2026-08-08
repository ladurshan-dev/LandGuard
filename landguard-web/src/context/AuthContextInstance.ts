import { createContext } from 'react';
import type { AuthContextValue } from '../types/auth';

/**
 * The raw React context object, isolated in its own file rather than
 * living in AuthContext.tsx alongside AuthProvider. Both AuthContext.tsx
 * (the component) and hooks/useAuth.ts (the hook) import it from here -
 * if either of them defined it directly, that file would export a mix of
 * a component and a non-component value, which is exactly what breaks
 * Vite Fast Refresh for every consumer (react-refresh/only-export-components).
 */
export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
