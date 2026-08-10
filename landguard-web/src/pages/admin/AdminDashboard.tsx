import { Alert, Box, Button, Paper, Typography } from '@mui/material';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import RateReviewIcon from '@mui/icons-material/RateReview';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../layouts/DashboardLayout';
import { useAuth } from '../../hooks/useAuth';

/**
 * User management (suspend/reactivate/verify NIC) remains a placeholder -
 * the backend exposes only raw stored procedures for it today
 * (usp_Admin_SetUserActive/usp_Admin_VerifyNIC), no REST endpoint yet.
 * Property moderation is no longer a placeholder: Property Reviews links
 * to the review queue (GET /api/admin/properties/review) where an Admin
 * can approve or reject a Pending listing, and Property Oversight
 * (view/inspect/delete, Module 4) remains available alongside it.
 */
export default function AdminDashboard() {
  const { user } = useAuth();

  if (!user) {
    return null;
  }

  return (
    <DashboardLayout title="Admin Dashboard" user={user}>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>
          Welcome, {user.name}
        </Typography>
        <Typography color="text.secondary" gutterBottom>
          Role: {user.role}
        </Typography>

        <Box sx={{ mt: 2, display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
          <Button variant="contained" startIcon={<RateReviewIcon />} component={RouterLink} to="/admin/properties/review">
            Property Reviews
          </Button>
          <Button variant="outlined" startIcon={<FactCheckIcon />} component={RouterLink} to="/admin/properties">
            Property Oversight
          </Button>
        </Box>

        <Alert severity="info" sx={{ mt: 3 }}>
          User management (suspend/reactivate accounts, verify NIC) isn't available yet - the backend doesn't
          currently expose endpoints for it.
        </Alert>
      </Paper>
    </DashboardLayout>
  );
}
