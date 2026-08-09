import type { ReactElement } from 'react';
import { Avatar, Box, Divider, List, ListItemButton, ListItemIcon, ListItemText, Typography } from '@mui/material';
import { alpha } from '@mui/material/styles';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import ShieldIcon from '@mui/icons-material/Shield';
import DashboardIcon from '@mui/icons-material/Dashboard';
import HomeWorkIcon from '@mui/icons-material/HomeWork';
import TravelExploreIcon from '@mui/icons-material/TravelExplore';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import { LogoutButton } from '../components/LogoutButton';
import { landguardColors } from '../theme/landguardTheme';
import type { AuthUser, UserRole } from '../types/auth';

interface NavItem {
  label: string;
  to: string;
  icon: ReactElement;
}

/**
 * Every `to` here is a route that already exists in AppRoutes.tsx - this
 * is deliberately not a place to add aspirational nav items (verification
 * history, profile, settings, notifications, analytics) for routes that
 * don't exist yet. Icons are reused from the exact ones each dashboard
 * placeholder's own action button already uses (HomeWorkIcon/
 * TravelExploreIcon/FactCheckIcon in SellerDashboard/BuyerDashboard/
 * AdminDashboard), so the icon a user sees in the sidebar is the same one
 * they already associate with that destination.
 */
const NAV_ITEMS_BY_ROLE: Record<UserRole, NavItem[]> = {
  Seller: [
    { label: 'Overview', to: '/seller/dashboard', icon: <DashboardIcon fontSize="small" /> },
    { label: 'My Properties', to: '/seller/properties', icon: <HomeWorkIcon fontSize="small" /> },
  ],
  Buyer: [
    { label: 'Overview', to: '/buyer/dashboard', icon: <DashboardIcon fontSize="small" /> },
    { label: 'Browse Properties', to: '/buyer/properties', icon: <TravelExploreIcon fontSize="small" /> },
  ],
  Admin: [
    { label: 'Overview', to: '/admin/dashboard', icon: <DashboardIcon fontSize="small" /> },
    { label: 'Property Oversight', to: '/admin/properties', icon: <FactCheckIcon fontSize="small" /> },
  ],
};

const ACTIVE_BG = alpha(landguardColors.gold, 0.18);
const ACTIVE_BG_HOVER = alpha(landguardColors.gold, 0.24);

interface SidebarNavProps {
  user: AuthUser;
  /** Called after a nav item is clicked - lets DashboardLayout's mobile temporary Drawer close itself. Omitted on the permanent desktop Drawer, which has nothing to close. */
  onNavigate?: () => void;
}

/**
 * Sidebar content shared by DashboardLayout's permanent (desktop) and
 * temporary (mobile) Drawer: brand mark, the signed-in user's name/role,
 * role-appropriate navigation with active-route highlighting, and the
 * existing LogoutButton (unmodified here - real useAuth().logout() plus
 * redirect, nothing about authentication reimplemented in this file).
 * A separate component (rather than inlined twice in DashboardLayout)
 * purely so the desktop and mobile Drawers render identical content
 * without duplicating this JSX.
 */
export function SidebarNav({ user, onNavigate }: SidebarNavProps) {
  const location = useLocation();
  const navItems = NAV_ITEMS_BY_ROLE[user.role];

  return (
    <Box
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: landguardColors.charcoal,
        color: 'rgba(255,255,255,0.75)',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 2.5, py: 3 }}>
        <Box
          sx={{
            width: 36,
            height: 36,
            borderRadius: '10px',
            bgcolor: 'secondary.main',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <ShieldIcon sx={{ color: 'secondary.contrastText', fontSize: 20 }} />
        </Box>
        <Typography variant="h6" component="span" sx={{ color: '#fff', fontWeight: 400 }}>
          LandGuard
        </Typography>
      </Box>

      <Divider sx={{ borderColor: 'rgba(255,255,255,0.08)' }} />

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 2.5, py: 2.5, minWidth: 0 }}>
        <Avatar
          sx={{ bgcolor: 'secondary.main', color: 'secondary.contrastText', width: 40, height: 40, fontWeight: 700 }}
        >
          {user.name.charAt(0).toUpperCase()}
        </Avatar>
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="body2" sx={{ color: '#fff', fontWeight: 600 }} noWrap>
            {user.name}
          </Typography>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.5)' }}>
            {user.role}
          </Typography>
        </Box>
      </Box>

      <Divider sx={{ borderColor: 'rgba(255,255,255,0.08)' }} />

      <List sx={{ flexGrow: 1, px: 1.5, py: 2 }}>
        {navItems.map((item) => {
          const isActive = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`);

          return (
            <ListItemButton
              key={item.to}
              component={RouterLink}
              to={item.to}
              onClick={onNavigate}
              selected={isActive}
              sx={{
                borderRadius: 2,
                mb: 0.5,
                color: 'inherit',
                '&.Mui-selected': {
                  bgcolor: ACTIVE_BG,
                  color: 'secondary.light',
                },
                '&.Mui-selected:hover': { bgcolor: ACTIVE_BG_HOVER },
                '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
              }}
            >
              <ListItemIcon sx={{ minWidth: 36, color: 'inherit' }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} slotProps={{ primary: { sx: { fontSize: 14, fontWeight: 500 } } }} />
            </ListItemButton>
          );
        })}
      </List>

      <Box sx={{ px: 1.5, py: 2, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
        <LogoutButton fullWidth />
      </Box>
    </Box>
  );
}
