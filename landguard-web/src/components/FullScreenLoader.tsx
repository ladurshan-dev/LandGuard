import { Box, CircularProgress } from '@mui/material';

interface FullScreenLoaderProps {
  label?: string;
}

/**
 * Centered full-viewport spinner - the shared "we don't know yet" state
 * shown while AuthContext is doing its one-time localStorage read on app
 * startup. Used by ProtectedRoute, LoginPage and AppRoutes's root
 * redirect, all of which need to show exactly this rather than exposing
 * (or redirecting away from) any content before isInitializing settles.
 */
export function FullScreenLoader({ label = 'Loading' }: FullScreenLoaderProps) {
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center' }}>
      <CircularProgress aria-label={label} />
    </Box>
  );
}
