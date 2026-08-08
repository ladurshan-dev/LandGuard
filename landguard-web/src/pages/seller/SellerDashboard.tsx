import { Box, Button, Paper, Typography } from '@mui/material';
import HomeWorkIcon from '@mui/icons-material/HomeWork';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../layouts/DashboardLayout';
import { useAuth } from '../../hooks/useAuth';

/** Fraud reports and OCR-based deed comparison remain placeholders for a later phase; property listing management (Module 4) now links through from here. */
export default function SellerDashboard() {
  const { user } = useAuth();

  if (!user) {
    // ProtectedRoute already guarantees a Seller-role user reaches this
    // component - guarded anyway so this file never assumes a non-null
    // user just because the router says it should be here.
    return null;
  }

  return (
    <DashboardLayout title="Seller Dashboard" user={user}>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>
          Welcome, {user.name}
        </Typography>
        <Typography color="text.secondary" gutterBottom>
          Role: {user.role}
        </Typography>

        <Box sx={{ mt: 2 }}>
          <Button variant="contained" startIcon={<HomeWorkIcon />} component={RouterLink} to="/seller/properties">
            Manage My Properties
          </Button>
        </Box>
      </Paper>
    </DashboardLayout>
  );
}
