import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import type { ChipProps } from '@mui/material';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import RateReviewIcon from '@mui/icons-material/RateReview';
import RefreshIcon from '@mui/icons-material/Refresh';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../layouts/DashboardLayout';
import { useAuth } from '../../hooks/useAuth';
import { PropertyStatusChip } from '../../components/property/PropertyStatusChip';
import { getFraudDashboard } from '../../services/adminService';
import { ApiError } from '../../utils/apiError';
import type { AdminFraudDashboardResponse } from '../../types/admin';
import type { RiskLevel } from '../../types/property';

const RISK_CHIP_COLOR: Record<RiskLevel, ChipProps['color']> = {
  Low: 'success',
  Medium: 'warning',
  High: 'error',
};

interface StatCardProps {
  label: string;
  value: string | number;
}

/** One small stat tile - the one presentational unit reused by every card section below (A-D). */
function StatCard({ label, value }: StatCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
        {label}
      </Typography>
      <Typography variant="h5" sx={{ fontWeight: 700 }}>
        {value}
      </Typography>
    </Paper>
  );
}

function SectionHeading({ children }: { children: string }) {
  return (
    <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5, mt: 4 }}>
      {children}
    </Typography>
  );
}

/**
 * Admin Fraud Dashboard - GET /api/admin/dashboard. Six read-only
 * sections layered on top of the existing "Property Reviews"/"Property
 * Oversight" shortcuts (unchanged below).
 *
 * IMPORTANT SEMANTICS: Risk Overview (section C) and Deed Verification
 * Overview (section B) are two independent systems and are kept visually
 * separate on purpose - High Risk does NOT mean Fraudulent, Low Risk does
 * NOT mean Verified. Risk cards report the legacy, per-property
 * weighted-rule fraud/risk engine (dbo.FraudCheck/RiskReport); Deed
 * Verification cards report the authoritative government
 * deed-verification outcome (dbo.DeedVerification, latest run per
 * property only). Property Status Overview (section A) is a third,
 * separate concept again - the listing's current workflow state, not a
 * fraud verdict.
 *
 * Privacy: nothing on this page ever renders Seller NIC, Owner NIC, deed
 * reference, OCR text, Government Registry values, or contact information -
 * the backend response itself never includes them (see
 * AdminDashboardReviewPropertySummary's own doc comment), so there is
 * nothing to accidentally display here.
 */
export default function AdminDashboard() {
  const { user } = useAuth();

  const [dashboard, setDashboard] = useState<AdminFraudDashboardResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Does not set isLoading itself - the initial call relies on useState(true)'s
  // own default (same split AdminPropertyReviewPage.loadQueue already
  // establishes), and the Refresh button sets it explicitly before calling
  // this, since setting state synchronously inside an effect body is what
  // this project's lint rules (react-hooks/set-state-in-effect) disallow.
  const loadDashboard = useCallback(() => {
    getFraudDashboard()
      .then((result) => {
        setDashboard(result);
        setLoadError(null);
      })
      .catch((error: unknown) => {
        setLoadError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  useEffect(() => {
    loadDashboard();
  }, [loadDashboard]);

  const handleRefresh = () => {
    setIsLoading(true);
    loadDashboard();
  };

  if (!user) {
    return null;
  }

  return (
    <DashboardLayout title="Admin Dashboard" user={user} maxWidth="lg">
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

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mt: 4, mb: 1, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h2">
          Fraud Dashboard
        </Typography>
        <Button startIcon={<RefreshIcon />} onClick={handleRefresh} disabled={isLoading}>
          Refresh
        </Button>
      </Box>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && dashboard && (
        <>
          {/* A. Property Status Overview */}
          <SectionHeading>Property Status Overview</SectionHeading>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Total" value={dashboard.properties.total} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Approved" value={dashboard.properties.approved} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Pending" value={dashboard.properties.pending} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Disapproved" value={dashboard.properties.disapproved} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Rejected" value={dashboard.properties.rejected} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Withdrawn" value={dashboard.properties.withdrawn} />
            </Grid>
            {dashboard.properties.flagged > 0 && (
              <Grid size={{ xs: 6, sm: 4, md: 2 }}>
                <StatCard label="Flagged" value={dashboard.properties.flagged} />
              </Grid>
            )}
          </Grid>

          {/* B. Deed Verification Overview */}
          <SectionHeading>Deed Verification Overview</SectionHeading>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5, mt: -1 }}>
            Latest verification run per property only - authoritative government registry outcome, not the legacy
            risk score below.
          </Typography>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Verified" value={dashboard.deedVerification.verified} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Form Mismatch" value={dashboard.deedVerification.formMismatch} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Registry Fraud / Material Mismatch" value={dashboard.deedVerification.fraudulent} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Price Anomaly" value={dashboard.deedVerification.priceAnomaly} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Duplicate Property" value={dashboard.deedVerification.duplicateProperty} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Unverified" value={dashboard.deedVerification.unverified} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Cancelled/Suspended Registry" value={dashboard.deedVerification.unverifiedCancelled} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <StatCard label="Total Verified Properties" value={dashboard.deedVerification.total} />
            </Grid>
          </Grid>

          <Grid container spacing={3} sx={{ mt: 0.5 }}>
            <Grid size={{ xs: 12, md: 6 }}>
              {/* C. Risk Overview */}
              <SectionHeading>Risk Overview</SectionHeading>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5, mt: -1 }}>
                Legacy weighted-rule fraud engine - a supporting indicator, not a verdict.
              </Typography>
              <Grid container spacing={2}>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Low Risk" value={dashboard.risk.low} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Medium Risk" value={dashboard.risk.medium} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="High Risk" value={dashboard.risk.high} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard
                    label="Avg. Risk Score"
                    value={dashboard.risk.averageRiskScore !== null ? dashboard.risk.averageRiskScore.toFixed(1) : 'N/A'}
                  />
                </Grid>
              </Grid>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              {/* D. User Overview */}
              <SectionHeading>User Overview</SectionHeading>
              <Grid container spacing={2} sx={{ mt: 0 }}>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Buyers" value={dashboard.users.totalBuyers} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Sellers" value={dashboard.users.totalSellers} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Verified Sellers" value={dashboard.users.verifiedSellers} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <StatCard label="Suspended Users" value={dashboard.users.suspendedUsers} />
                </Grid>
              </Grid>
            </Grid>
          </Grid>

          {/* E. Fraud Rule Trigger Frequency */}
          <SectionHeading>Fraud Rule Trigger Frequency</SectionHeading>
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Rule</TableCell>
                  <TableCell align="right">Weight</TableCell>
                  <TableCell align="right">Times Triggered</TableCell>
                  <TableCell align="right">Times Evaluated</TableCell>
                  <TableCell align="right">Trigger Rate</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {dashboard.ruleTriggers.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <Typography color="text.secondary" variant="body2">
                        No rule data available.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {dashboard.ruleTriggers.map((rule) => (
                  <TableRow key={rule.ruleCode}>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {rule.ruleName}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {rule.ruleCode}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">{rule.weight}</TableCell>
                    <TableCell align="right">{rule.timesTriggered}</TableCell>
                    <TableCell align="right">{rule.timesEvaluated}</TableCell>
                    <TableCell align="right">
                      {rule.triggerRatePercent !== null ? `${rule.triggerRatePercent.toFixed(1)}%` : '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {/* F. Requires Attention */}
          <SectionHeading>Requires Attention</SectionHeading>
          <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Property</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Risk Score</TableCell>
                  <TableCell>Risk Level</TableCell>
                  <TableCell align="right">Days Waiting</TableCell>
                  <TableCell align="right">Open Reports</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {dashboard.reviewProperties.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <Typography color="text.secondary" variant="body2">
                        Nothing is waiting for review right now.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {dashboard.reviewProperties.map((item) => (
                  <TableRow key={item.propertyId} hover>
                    <TableCell>
                      <Typography
                        component={RouterLink}
                        to={`/admin/properties/${item.propertyId}`}
                        variant="body2"
                        sx={{ fontWeight: 600, color: 'inherit', textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }}
                      >
                        {item.title}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        #{item.propertyId}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <PropertyStatusChip status={item.status} />
                    </TableCell>
                    <TableCell align="right">{item.riskScore !== null ? item.riskScore : '—'}</TableCell>
                    <TableCell>
                      <Chip label={item.riskLevel} color={RISK_CHIP_COLOR[item.riskLevel]} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell align="right">{item.daysWaiting}</TableCell>
                    <TableCell align="right">{item.openReportCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}
    </DashboardLayout>
  );
}
