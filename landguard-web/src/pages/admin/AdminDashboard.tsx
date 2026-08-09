import { Alert, Box, Button, Paper, Typography } from '@mui/material';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../layouts/DashboardLayout';
import { useAuth } from '../../hooks/useAuth';

/**
 * Property approve/reject/flag and user management remain placeholders -
 * the backend currently exposes no REST endpoints for either (only raw
 * stored procedures), so this dashboard cannot link to functionality that
 * doesn't exist yet. Property oversight (view/inspect/delete, Module 4)
 * now links through from here.
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

        <Box sx={{ mt: 2 }}>
          <Button variant="contained" startIcon={<FactCheckIcon />} component={RouterLink} to="/admin/properties">
            Property Oversight
          </Button>
        </Box>

        <Alert severity="info" sx={{ mt: 3 }}>
          Approve/reject/flag actions and user management aren't available yet - the backend doesn't currently expose
          endpoints for them.
        </Alert>
      </Paper>
    </DashboardLayout>
  );
}
