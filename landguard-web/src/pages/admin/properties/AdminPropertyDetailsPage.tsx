import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Grid,
  Paper,
  Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import DeleteIcon from '@mui/icons-material/Delete';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { PropertyImageGallery } from '../../../components/property/PropertyImageGallery';
import { PropertyFraudPanel } from '../../../components/property/PropertyFraudPanel';
import { deleteProperty, getPropertyById } from '../../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail } from '../../../types/property';

/**
 * Admin's view of any single property, regardless of status - the one
 * place this app's Admin role can reach a Pending/Flagged/Rejected
 * listing, since PropertyService.GetByIdAsync grants an Admin caller
 * visibility of any status while GET /api/properties (search) does not.
 * Delete is the only mutating action available - usp_Property_Delete
 * itself allows the owner or an Admin, but there is no Update access for
 * Admin (PUT /api/properties/{id} is behind the RequireSeller policy) and
 * no approve/reject/flag endpoint exists at all today, so neither is
 * offered here.
 */
export default function AdminPropertyDetailsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

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

  const handleConfirmDelete = async () => {
    setIsDeleting(true);
    setDeleteError(null);

    try {
      await deleteProperty(propertyId);
      navigate('/admin/properties', { replace: true });
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      setIsDeleting(false);
    }
  };

  return (
    <DashboardLayout title="Property Details" user={user} maxWidth="md">
      <Button startIcon={<ArrowBackIcon />} component={RouterLink} to="/admin/properties" sx={{ mb: 2 }}>
        Back to Property Oversight
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
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2, flexWrap: 'wrap' }}>
                <Box>
                  <Typography variant="h5" component="h1">
                    {detail.listing.title}
                  </Typography>
                  <Typography color="text.secondary">
                    {detail.listing.location}
                    {detail.listing.district ? `, ${detail.listing.district}` : ''}
                  </Typography>
                </Box>
                <PropertyStatusChip status={detail.listing.status} size="medium" />
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
                <Typography variant="caption" color="text.secondary">
                  Seller ID: {detail.listing.sellerId} &middot; Reports against this listing: {detail.listing.reportCount}
                </Typography>
              </Box>

              <Box sx={{ mt: 3 }}>
                <Button
                  variant="outlined"
                  color="error"
                  startIcon={<DeleteIcon />}
                  onClick={() => {
                    setDeleteError(null);
                    setIsDeleteDialogOpen(true);
                  }}
                >
                  Delete Listing
                </Button>
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

      <Dialog open={isDeleteDialogOpen} onClose={() => setIsDeleteDialogOpen(false)}>
        <DialogTitle>Delete this property?</DialogTitle>
        <DialogContent>
          <DialogContentText>This permanently removes the listing and cannot be undone.</DialogContentText>
          {deleteError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {deleteError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIsDeleteDialogOpen(false)} disabled={isDeleting}>
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => void handleConfirmDelete()}
            disabled={isDeleting}
            startIcon={isDeleting ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </DashboardLayout>
  );
}
