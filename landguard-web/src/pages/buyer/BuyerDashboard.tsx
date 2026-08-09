import { Box, Button, Paper, Typography } from '@mui/material';
import TravelExploreIcon from '@mui/icons-material/TravelExplore';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../layouts/DashboardLayout';
import { useAuth } from '../../hooks/useAuth';

/** Saved listings and suspicious-listing reports remain placeholders for a later phase; property search/browsing (Module 4) now links through from here. */
export default function BuyerDashboard() {
  const { user } = useAuth();

  if (!user) {
    return null;
  }

  return (
    <DashboardLayout title="Buyer Dashboard" user={user}>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>
          Welcome, {user.name}
        </Typography>
        <Typography color="text.secondary" gutterBottom>
          Role: {user.role}
        </Typography>

        <Box sx={{ mt: 2 }}>
          <Button variant="contained" startIcon={<TravelExploreIcon />} component={RouterLink} to="/buyer/properties">
            Browse Properties
          </Button>
        </Box>
      </Paper>
    </DashboardLayout>
  );
}
