import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Grid, Paper, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PlaceIcon from '@mui/icons-material/Place';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyImageGallery } from '../../../components/property/PropertyImageGallery';
import { PropertyFraudPanel } from '../../../components/property/PropertyFraudPanel';
import { getPropertyById } from '../../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail } from '../../../types/property';

/**
 * A buyer's read-only view of one property. No edit/delete/status
 * controls - a buyer can only ever reach an Approved listing here anyway
 * (PropertyService.GetByIdAsync returns the same "Property not found" for
 * a non-Approved id as for a nonexistent one unless the caller is the
 * owner or an Admin), so this page never needs its own visibility check.
 */
export default function BuyerPropertyDetailsPage() {
  const { user } = useAuth();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Promise-chained - see SellerPropertiesPage.loadListings for why.
  const loadDetail = useCallback((propertyIdToLoad: number) => {
    getPropertyById(propertyIdToLoad)
      .then((result) => {
        setDetail(result);
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
    if (propertyId !== null) {
      loadDetail(propertyId);
    }
  }, [propertyId, loadDetail]);

  if (!user || propertyId === null) {
    return null;
  }

  return (
    <DashboardLayout title="Property Details" user={user} maxWidth="md">
      <Button startIcon={<ArrowBackIcon />} component={RouterLink} to="/buyer/properties" sx={{ mb: 2 }}>
        Back to Browse
      </Button>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && detail && (
        <Grid container spacing={3}>
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 3 }}>
              <Typography variant="h5" component="h1">
                {detail.listing.title}
              </Typography>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: 'text.secondary', mt: 0.5 }}>
                <PlaceIcon fontSize="small" />
                <Typography variant="body2">
                  {detail.listing.location}
                  {detail.listing.district ? `, ${detail.listing.district}` : ''}
                </Typography>
              </Box>

              {detail.listing.description && <Typography sx={{ mt: 2 }}>{detail.listing.description}</Typography>}

              <Grid container spacing={2} sx={{ mt: 1 }}>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <Typography variant="caption" color="text.secondary">Price</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatCurrency(detail.listing.price)}</Typography>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <Typography variant="caption" color="text.secondary">Size</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatSize(detail.listing.size)}</Typography>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <Typography variant="caption" color="text.secondary">Deed Reference</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>{detail.listing.deedReference ?? '-'}</Typography>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <Typography variant="caption" color="text.secondary">Listed On</Typography>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatDate(detail.listing.uploadDate)}</Typography>
                </Grid>
              </Grid>

              <Box sx={{ mt: 2, pt: 2, borderTop: 1, borderColor: 'divider' }}>
                <Typography variant="subtitle2" color="text.secondary">Seller</Typography>
                <Typography variant="body1">
                  {detail.listing.sellerName}
                  {detail.listing.sellerNicVerified ? ' (NIC verified)' : ''}
                </Typography>
                {detail.listing.sellerPhone && (
                  <Typography variant="body2" color="text.secondary">{detail.listing.sellerPhone}</Typography>
                )}
              </Box>
            </Paper>
          </Grid>

          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
                Images
              </Typography>
              <PropertyImageGallery images={detail.images} />
            </Paper>
          </Grid>

          <Grid size={12}>
            <PropertyFraudPanel
              riskLevel={detail.listing.riskLevel}
              fraudStatus={detail.listing.fraudStatus}
              riskScore={detail.listing.riskScore}
              riskSummary={detail.listing.riskSummary}
              riskGeneratedDate={detail.listing.riskGeneratedDate}
              fraudReport={detail.fraudReport}
            />
          </Grid>
        </Grid>
      )}
    </DashboardLayout>
  );
}
