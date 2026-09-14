import { Box, Container, Divider, Link, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import ShieldIcon from '@mui/icons-material/Shield';
import { useAuth } from '../../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../../types/auth';
import { landguardColors } from '../../theme/landguardTheme';

/**
 * Public footer for HomePage/PublicBrowsePropertiesPage/
 * PublicPropertyDetailsPage. No fabricated contact details (no phone/
 * email/address exist anywhere in this project to show truthfully) and no
 * links to pages that don't exist (no "Safety"/"About" route was built -
 * see HomeSafety's own doc comment for why). The auth-related link is
 * role-aware for the same reason PublicNavbar's is: a signed-in visitor
 * should never see a "Sign In" link for the account they're already using.
 */
export function PublicFooter() {
  const { isAuthenticated, user } = useAuth();

  return (
    <Box component="footer" sx={{ bgcolor: landguardColors.charcoal, color: 'rgba(255,255,255,0.75)', mt: 'auto' }}>
      <Container maxWidth="lg" sx={{ py: 5 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ justifyContent: 'space-between' }}>
          <Box sx={{ maxWidth: 380 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
              <ShieldIcon sx={{ color: landguardColors.goldLight }} />
              <Typography variant="h6" sx={{ color: '#fff', fontWeight: 400 }}>
                LandGuard
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.6)' }}>
              A land property platform for Sri Lanka that checks every listing's deed against government
              registry records and requires administrator approval before it becomes visible to buyers.
            </Typography>
          </Box>

          <Stack spacing={1}>
            <Typography variant="subtitle2" sx={{ color: '#fff' }}>
              Explore
            </Typography>
            <Link component={RouterLink} to="/" color="inherit" underline="hover" sx={{ opacity: 0.75 }}>
              Home
            </Link>
            <Link component={RouterLink} to="/properties" color="inherit" underline="hover" sx={{ opacity: 0.75 }}>
              Browse Properties
            </Link>
            {isAuthenticated && user ? (
              <Link
                component={RouterLink}
                to={DASHBOARD_PATH_BY_ROLE[user.role]}
                color="inherit"
                underline="hover"
                sx={{ opacity: 0.75 }}
              >
                Dashboard
              </Link>
            ) : (
              <>
                <Link component={RouterLink} to="/login" color="inherit" underline="hover" sx={{ opacity: 0.75 }}>
                  Sign In
                </Link>
                <Link component={RouterLink} to="/register" color="inherit" underline="hover" sx={{ opacity: 0.75 }}>
                  Create Account
                </Link>
              </>
            )}
          </Stack>
        </Stack>

        <Divider sx={{ my: 3, borderColor: 'rgba(255,255,255,0.1)' }} />

        <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.5)' }}>
          &copy; {new Date().getFullYear()} LandGuard. All property listings are reviewed before publication.
        </Typography>
      </Container>
    </Box>
  );
}
