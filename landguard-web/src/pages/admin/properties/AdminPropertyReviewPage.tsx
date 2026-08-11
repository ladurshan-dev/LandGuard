import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import RateReviewIcon from '@mui/icons-material/RateReview';
import RefreshIcon from '@mui/icons-material/Refresh';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { RiskIndicator } from '../../../components/property/RiskIndicator';
import { getPropertyReviewQueue } from '../../../services/adminService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyReviewQueueItem } from '../../../types/admin';

/**
 * The admin review queue - GET /api/admin/properties/review. Distinct from
 * AdminPropertiesPage ("Property Oversight", unmodified by this phase),
 * which browses Approved listings the same way a buyer would and looks up
 * any single property by id; this page's whole purpose is the opposite:
 * every property that still needs a human decision, normally
 * Status = Pending since Phase C (plus any legacy Flagged rows or
 * anything with an open suspicious report - see the backend's own doc
 * comment on IAdminModerationService.GetReviewQueueAsync).
 *
 * PROPERTY STATUS and RISK ASSESSMENT are shown as two clearly separate
 * chips/indicators on every card - Status = Pending and Risk = High is
 * an expected, valid combination here, not a contradiction. Nothing on
 * this page infers or displays an approval/rejection decision from the
 * risk indicators; the actual decision happens on the property's own
 * review screen (AdminPropertyDetailsPage's "Admin Decision" section).
 */
export default function AdminPropertyReviewPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [items, setItems] = useState<PropertyReviewQueueItem[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Does not set isLoading itself - the initial call relies on useState(true)'s
  // own default (see AdminPropertyDetailsPage.loadDetail for the identical
  // split), and the Refresh button below sets it explicitly before calling
  // this, since setting state synchronously inside an effect body is what
  // this project's lint rules (react-hooks/set-state-in-effect) disallow.
  const loadQueue = useCallback(() => {
    getPropertyReviewQueue()
      .then((result) => {
        setItems(result);
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
    loadQueue();
  }, [loadQueue]);

  const handleRefresh = () => {
    setIsLoading(true);
    loadQueue();
  };

  if (!user) {
    return null;
  }

  return (
    <DashboardLayout title="Property Reviews" user={user} maxWidth="lg">
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1">
          Property Reviews
        </Typography>
        <Button startIcon={<RefreshIcon />} onClick={handleRefresh} disabled={isLoading}>
          Refresh
        </Button>
      </Box>

      <Alert severity="info" icon={<RateReviewIcon fontSize="small" />} sx={{ mb: 3 }}>
        Risk information below is a supporting indicator, not a verdict. A property can show <strong>Status:
        Pending</strong> alongside <strong>Risk: High</strong> - review the evidence and decide manually. Nothing is
        approved or rejected automatically.
      </Alert>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && items && items.length === 0 && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">Nothing is waiting for review right now.</Typography>
        </Paper>
      )}

      {!isLoading && !loadError && items && items.length > 0 && (
        <Stack spacing={2}>
          {items.map((item) => (
            <Card key={item.propertyId} variant="outlined">
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2, flexWrap: 'wrap' }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="caption" color="text.secondary">
                      Property #{item.propertyId}
                    </Typography>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                      {item.title}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {item.location}
                      {item.district ? `, ${item.district}` : ''} &middot; {formatSize(item.size)}
                    </Typography>
                  </Box>

                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexShrink: 0 }}>
                    <PropertyStatusChip status={item.status} />
                    {item.openReportCount > 0 && (
                      <Chip label={`${item.openReportCount} open report${item.openReportCount === 1 ? '' : 's'}`} color="warning" size="small" variant="outlined" />
                    )}
                  </Stack>
                </Box>

                <Divider sx={{ my: 1.5 }} />

                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1.5 }}>
                  <Box>
                    <Typography variant="body2" color="text.secondary">
                      Seller: {item.sellerName}
                      {item.sellerNicVerified ? ' (NIC verified)' : ''}
                    </Typography>
                    <Typography variant="h6" color="primary" sx={{ fontWeight: 700 }}>
                      {formatCurrency(item.price)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      Listed {formatDate(item.uploadDate)} &middot; waiting {item.daysWaiting} day{item.daysWaiting === 1 ? '' : 's'}
                    </Typography>
                  </Box>

                  <Box sx={{ textAlign: { xs: 'left', sm: 'right' } }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                      Risk assessment (supporting indicator)
                    </Typography>
                    <RiskIndicator riskLevel={item.riskLevel} fraudStatus={item.fraudStatus} riskScore={item.riskScore} />
                  </Box>
                </Box>

                <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
                  <Button variant="contained" onClick={() => navigate(`/admin/properties/${item.propertyId}`)}>
                    Review Property
                  </Button>
                </Box>
              </CardContent>
            </Card>
          ))}
        </Stack>
      )}
    </DashboardLayout>
  );
}
