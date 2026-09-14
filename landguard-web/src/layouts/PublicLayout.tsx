import type { ReactNode } from 'react';
import { Box } from '@mui/material';
import { PublicNavbar } from '../components/home/PublicNavbar';
import { PublicFooter } from '../components/home/PublicFooter';

interface PublicLayoutProps {
  children: ReactNode;
}

/**
 * Shell for every public (non-dashboard) page: HomePage,
 * PublicBrowsePropertiesPage, PublicPropertyDetailsPage. Deliberately not
 * DashboardLayout - there is no permanent sidebar, no signed-in-user
 * requirement, and it renders identically for an anonymous visitor and a
 * signed-in Buyer/Seller/Admin who navigates here (PublicNavbar reacts to
 * auth state on its own; this shell does not need to).
 */
export function PublicLayout({ children }: PublicLayoutProps) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', bgcolor: 'background.default' }}>
      <PublicNavbar />
      <Box component="main" sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column' }}>
        {children}
      </Box>
      <PublicFooter />
    </Box>
  );
}
