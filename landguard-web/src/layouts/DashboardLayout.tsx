import { useState } from 'react';
import type { ReactNode } from 'react';
import { AppBar, Box, Container, Drawer, IconButton, Toolbar, Typography } from '@mui/material';
import type { ContainerProps } from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import ShieldIcon from '@mui/icons-material/Shield';
import { SidebarNav } from './SidebarNav';
import type { AuthUser } from '../types/auth';

const DRAWER_WIDTH = 260;

interface DashboardLayoutProps {
  /** Which dashboard is active - shown in the content header so it's always visually unambiguous which role's view is on screen. */
  title: string;
  user: AuthUser;
  children: ReactNode;
  /** Content container width - defaults to "md" (the original dashboards' width). Property list/grid screens pass "lg" for more breathing room; this stays optional so every existing call site is unaffected. */
  maxWidth?: ContainerProps['maxWidth'];
}

/**
 * Shared shell for the three role dashboards and every property page
 * (Stage 2 visual redesign - see the attached design reference approved
 * for this file). Same props as before this redesign
 * (title/user/children/maxWidth) - every existing caller needed zero
 * changes.
 *
 * Structure: a permanent MUI Drawer on desktop (md and up) holding
 * <SidebarNav> (branding, the signed-in user, role-appropriate
 * navigation, logout - see that file's own doc comment), and a temporary
 * (hamburger-triggered) Drawer with the identical content on mobile, so
 * the desktop sidebar is never squeezed rather than hidden. The content
 * area keeps the existing Container/maxWidth contract untouched -
 * everything passed as `children` renders exactly as before, just inside
 * a re-themed shell.
 */
export function DashboardLayout({ title, user, children, maxWidth = 'md' }: DashboardLayoutProps) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      {/* Mobile-only topbar - hidden from md upward so it never appears alongside the permanent desktop sidebar. */}
      <AppBar
        position="fixed"
        color="primary"
        sx={{ display: { xs: 'block', md: 'none' }, zIndex: (theme) => theme.zIndex.drawer + 1 }}
      >
        <Toolbar sx={{ gap: 1.5 }}>
          <IconButton
            color="inherit"
            edge="start"
            onClick={() => setMobileOpen(true)}
            aria-label="Open navigation menu"
          >
            <MenuIcon />
          </IconButton>
          <ShieldIcon />
          <Typography variant="h6" component="span" sx={{ fontWeight: 400 }}>
            LandGuard
          </Typography>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: 'block', md: 'none' },
            '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box', border: 'none' },
          }}
        >
          <SidebarNav user={user} onNavigate={() => setMobileOpen(false)} />
        </Drawer>

        <Drawer
          variant="permanent"
          open
          sx={{
            display: { xs: 'none', md: 'block' },
            '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box', border: 'none' },
          }}
        >
          <SidebarNav user={user} />
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          display: 'flex',
          flexDirection: 'column',
          mt: { xs: 7, md: 0 },
        }}
      >
        <Box
          sx={{
            px: { xs: 2.5, md: 4 },
            py: { xs: 2, md: 3 },
            bgcolor: 'background.paper',
            borderBottom: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Typography variant="h5" component="h1">
            {title}
          </Typography>
        </Box>

        <Container maxWidth={maxWidth} sx={{ py: 4, flexGrow: 1 }}>
          {children}
        </Container>
      </Box>
    </Box>
  );
}
