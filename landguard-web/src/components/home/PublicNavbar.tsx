import { useState } from 'react';
import {
  AppBar,
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import MenuIcon from '@mui/icons-material/Menu';
import ShieldIcon from '@mui/icons-material/Shield';
import { useAuth } from '../../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../../types/auth';
import { landguardColors } from '../../theme/landguardTheme';

interface NavLinkItem {
  label: string;
  to: string;
}

/**
 * The exact, explicitly-specified nav item sets per visitor state - not a
 * derived/aspirational list. Each array is deliberately different in
 * length (Admin has no "Browse Properties", unauthenticated has no
 * "Dashboard") rather than one superset filtered down, so it stays
 * obviously in sync with what was actually requested.
 */
function useNavLinks(): NavLinkItem[] {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated || !user) {
    return [
      { label: 'Home', to: '/' },
      { label: 'Browse Properties', to: '/properties' },
    ];
  }

  if (user.role === 'Buyer') {
    return [
      { label: 'Home', to: '/' },
      { label: 'Browse Properties', to: '/properties' },
      { label: 'Dashboard', to: DASHBOARD_PATH_BY_ROLE.Buyer },
    ];
  }

  if (user.role === 'Seller') {
    return [
      { label: 'Home', to: '/' },
      { label: 'Browse Properties', to: '/properties' },
      { label: 'List Property', to: '/seller/properties/new' },
      { label: 'Dashboard', to: DASHBOARD_PATH_BY_ROLE.Seller },
    ];
  }

  // Admin - Home and Dashboard only, exactly as specified (no Browse
  // Properties link here; an Admin's own "Property Oversight"/"Property
  // Reviews" sidebar items already cover that from inside the dashboard).
  return [
    { label: 'Home', to: '/' },
    { label: 'Dashboard', to: DASHBOARD_PATH_BY_ROLE.Admin },
  ];
}

/**
 * Public top navigation bar - shown on HomePage, PublicBrowsePropertiesPage
 * and PublicPropertyDetailsPage regardless of authentication state (unlike
 * DashboardLayout's Drawer-based SidebarNav, which only ever renders for a
 * signed-in user inside a protected route). Role-aware per an explicit,
 * exact specification: Unauthenticated gets Sign In/Create Account action
 * buttons instead of a Dashboard link; every authenticated role gets a
 * single "Dashboard" link that reuses DASHBOARD_PATH_BY_ROLE, never a
 * hand-rolled per-role path.
 */
export function PublicNavbar() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const navLinks = useNavLinks();
  const [mobileOpen, setMobileOpen] = useState(false);

  const isActive = (to: string) => location.pathname === to || (to !== '/' && location.pathname.startsWith(`${to}/`));

  return (
    <AppBar position="sticky" color="inherit" sx={{ bgcolor: 'background.paper', borderBottom: '1px solid', borderColor: 'divider' }}>
      <Toolbar sx={{ gap: 2 }}>
        <Box
          component={RouterLink}
          to="/"
          sx={{ display: 'flex', alignItems: 'center', gap: 1, textDecoration: 'none', color: 'inherit', mr: 1 }}
        >
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: '9px',
              bgcolor: 'primary.main',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <ShieldIcon sx={{ color: '#fff', fontSize: 19 }} />
          </Box>
          <Typography variant="h6" component="span" sx={{ fontWeight: 400, color: 'text.primary' }}>
            LandGuard
          </Typography>
        </Box>

        {/* Desktop links */}
        <Stack direction="row" spacing={0.5} sx={{ display: { xs: 'none', md: 'flex' }, flexGrow: 1 }}>
          {navLinks.map((item) => (
            <Button
              key={item.to}
              component={RouterLink}
              to={item.to}
              color="inherit"
              sx={{
                fontWeight: isActive(item.to) ? 700 : 500,
                color: isActive(item.to) ? 'primary.main' : 'text.secondary',
              }}
            >
              {item.label}
            </Button>
          ))}
        </Stack>

        <Box sx={{ flexGrow: { xs: 1, md: 0 } }} />

        {!isAuthenticated && (
          <Stack direction="row" spacing={1} sx={{ display: { xs: 'none', md: 'flex' } }}>
            <Button component={RouterLink} to="/login" variant="outlined">
              Sign In
            </Button>
            <Button component={RouterLink} to="/register" variant="contained">
              Create Account
            </Button>
          </Stack>
        )}

        <IconButton
          color="inherit"
          edge="end"
          aria-label="Open navigation menu"
          onClick={() => setMobileOpen(true)}
          sx={{ display: { xs: 'inline-flex', md: 'none' } }}
        >
          <MenuIcon />
        </IconButton>
      </Toolbar>

      <Drawer anchor="right" open={mobileOpen} onClose={() => setMobileOpen(false)}>
        <Box sx={{ width: 260, pt: 2 }} role="presentation">
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 2, pb: 1 }}>
            <ShieldIcon color="primary" />
            <Typography variant="h6" sx={{ fontWeight: 400 }}>
              LandGuard
            </Typography>
          </Box>
          <Divider />
          <List>
            {navLinks.map((item) => (
              <ListItemButton
                key={item.to}
                component={RouterLink}
                to={item.to}
                selected={isActive(item.to)}
                onClick={() => setMobileOpen(false)}
                sx={{ '&.Mui-selected': { bgcolor: `${landguardColors.goldPale}` } }}
              >
                <ListItemText primary={item.label} />
              </ListItemButton>
            ))}
          </List>

          {!isAuthenticated && (
            <>
              <Divider />
              <Stack spacing={1.5} sx={{ p: 2 }}>
                <Button
                  component={RouterLink}
                  to="/login"
                  variant="outlined"
                  fullWidth
                  onClick={() => setMobileOpen(false)}
                >
                  Sign In
                </Button>
                <Button
                  component={RouterLink}
                  to="/register"
                  variant="contained"
                  fullWidth
                  onClick={() => setMobileOpen(false)}
                >
                  Create Account
                </Button>
              </Stack>
            </>
          )}
        </Box>
      </Drawer>
    </AppBar>
  );
}
