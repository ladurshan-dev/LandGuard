import type { ReactNode } from 'react';
import { AppBar, Box, Chip, Container, Toolbar, Typography } from '@mui/material';
import type { ContainerProps } from '@mui/material';
import ShieldIcon from '@mui/icons-material/Shield';
import { LogoutButton } from '../components/LogoutButton';
import type { AuthUser } from '../types/auth';

interface DashboardLayoutProps {
  /** Which dashboard is active - shown in the top bar so it's always visually unambiguous which role's view is on screen. */
  title: string;
  user: AuthUser;
  children: ReactNode;
  /** Content container width - defaults to "md" (the original dashboards' width). Property list/grid screens pass "lg" for more breathing room; this stays optional so every existing call site is unaffected. */
  maxWidth?: ContainerProps['maxWidth'];
}

/**
 * Shared shell for the three role dashboards - just enough chrome (a top
 * bar naming the active dashboard, the logged-in user's name/role, and
 * logout) to satisfy the placeholder requirements without three
 * near-identical copies of the same AppBar/Toolbar JSX. Lives in
 * layouts/, not components/, since it's specifically page-level
 * scaffolding rather than a reusable widget. The dashboards themselves
 * stay free to grow independently later - this only owns the shell
 * around them.
 */
export function DashboardLayout({ title, user, children, maxWidth = 'md' }: DashboardLayoutProps) {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.100' }}>
      <AppBar position="static" color="primary" elevation={1}>
        <Toolbar sx={{ gap: 2 }}>
          <ShieldIcon />
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            LandGuard — {title}
          </Typography>
          <Chip label={user.role} size="small" sx={{ bgcolor: 'common.white' }} />
          <Typography variant="body2">{user.name}</Typography>
          <LogoutButton />
        </Toolbar>
      </AppBar>

      <Container maxWidth={maxWidth} sx={{ py: 4 }}>
        {children}
      </Container>
    </Box>
  );
}
