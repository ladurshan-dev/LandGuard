import { useCallback, useEffect, useRef, useState } from 'react';
import { Link as RouterLink, useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControlLabel,
  Grid,
  Paper,
  Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import UploadIcon from '@mui/icons-material/Upload';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { PropertyImageGallery } from '../../../components/property/PropertyImageGallery';
import { PropertyFraudPanel } from '../../../components/property/PropertyFraudPanel';
import { SellerDeedVerificationSection } from '../../../components/property/SellerDeedVerificationSection';
import { deletePropertyImage, getPropertyById, uploadPropertyImage, withdrawProperty } from '../../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail } from '../../../types/property';

/** The seller's view of one of their own properties: full detail, images, fraud report, plus edit/delete/upload actions. */
export default function SellerPropertyDetailsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Set only by PropertyFormPage's create-mode navigation, when property
  // creation succeeded but the immediate deed upload/verification failed
  // (see that page's onSubmit) - the property itself (this page) still
  // exists and stays Pending; the Deed Verification section below already
  // shows its own "not completed yet" state with the same upload control
  // as the retry path, so this banner only needs to explain why the seller
  // landed here instead of silently repeating that state.
  const deedVerificationFailedOnCreate = Boolean(
    (location.state as { deedVerificationFailed?: boolean } | null)?.deedVerificationFailed,
  );

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [isWithdrawDialogOpen, setIsWithdrawDialogOpen] = useState(false);
  const [isWithdrawing, setIsWithdrawing] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  const [isPrimaryImage, setIsPrimaryImage] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const [deletingImageId, setDeletingImageId] = useState<number | null>(null);
  const [deleteImageError, setDeleteImageError] = useState<string | null>(null);
  const [deleteImageSuccess, setDeleteImageSuccess] = useState(false);

  // Same constraint (and same promise-chained fix) as
  // SellerPropertiesPage.loadListings - see that file's comment.
  const loadDetail = useCallback((id: number) => {
    getPropertyById(id)
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

  const isOwner = detail?.listing.sellerId === user.userId;
  const isWithdrawn = detail?.listing.status === 'Withdrawn';

  const handleConfirmWithdraw = async () => {
    setIsWithdrawing(true);
    setWithdrawError(null);

    try {
      await withdrawProperty(propertyId);
      // Navigate back to the list rather than faking the status change here
      // - SellerPropertiesPage re-fetches from the backend on mount, so the
      // listing reappears there with its real, authoritative Withdrawn
      // status instead of a client-guessed one.
      navigate('/seller/properties', { replace: true });
    } catch (error) {
      setWithdrawError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      setIsWithdrawing(false);
    }
  };

  const handleUpload = async () => {
    const file = fileInputRef.current?.files?.[0];

    if (!file) {
      setUploadError('Choose an image file first.');
      return;
    }

    setIsUploading(true);
    setUploadError(null);

    try {
      const updated = await uploadPropertyImage(propertyId, { file, isPrimary: isPrimaryImage });
      setDetail(updated);
      setIsPrimaryImage(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    } catch (error) {
      setUploadError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsUploading(false);
    }
  };

  const handleDeleteImage = async (imageId: number) => {
    setDeletingImageId(imageId);
    setDeleteImageError(null);
    setDeleteImageSuccess(false);

    try {
      const updated = await deletePropertyImage(propertyId, imageId);
      // Only replace the displayed detail on success - a failed delete
      // must leave the currently-shown image in place (see the
      // catch block below, which touches nothing but the error message).
      setDetail(updated);
      setDeleteImageSuccess(true);
    } catch (error) {
      setDeleteImageError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setDeletingImageId(null);
    }
  };

  return (
    <DashboardLayout title="Property Details" user={user} maxWidth="md">
      <Button startIcon={<ArrowBackIcon />} component={RouterLink} to="/seller/properties" sx={{ mb: 2 }}>
        Back to My Properties
      </Button>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && detail && !isOwner && (
        <Alert severity="error">Property not found.</Alert>
      )}

      {!isLoading && !loadError && detail && isOwner && deedVerificationFailedOnCreate && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Property created successfully, but deed verification could not be completed. Upload your deed document
          below to try again.
        </Alert>
      )}

      {!isLoading && !loadError && detail && isOwner && (
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

              {detail.listing.description && (
                <Typography sx={{ mt: 2 }}>{detail.listing.description}</Typography>
              )}

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

              <Box sx={{ display: 'flex', gap: 2, mt: 3 }}>
                {!isWithdrawn && (
                  <Button
                    variant="outlined"
                    startIcon={<EditIcon />}
                    component={RouterLink}
                    to={`/seller/properties/${propertyId}/edit`}
                  >
                    Edit
                  </Button>
                )}
                {!isWithdrawn && (
                  <Button
                    variant="outlined"
                    color="error"
                    startIcon={<DeleteIcon />}
                    onClick={() => {
                      setWithdrawError(null);
                      setIsWithdrawDialogOpen(true);
                    }}
                  >
                    Withdraw Listing
                  </Button>
                )}
                {isWithdrawn && (
                  <Typography color="text.secondary">
                    This listing has been withdrawn and is no longer editable or visible to buyers.
                  </Typography>
                )}
              </Box>
            </Paper>
          </Grid>

          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
                Images
              </Typography>

              {deleteImageSuccess && (
                <Alert severity="success" sx={{ mb: 2 }} onClose={() => setDeleteImageSuccess(false)}>
                  Image deleted.
                </Alert>
              )}
              {deleteImageError && (
                <Alert severity="error" sx={{ mb: 2 }} onClose={() => setDeleteImageError(null)}>
                  {deleteImageError}
                </Alert>
              )}

              <PropertyImageGallery
                images={detail.images}
                onDeleteImage={(imageId) => void handleDeleteImage(imageId)}
                deletingImageId={deletingImageId}
              />

              <Box sx={{ mt: 3, pt: 2, borderTop: 1, borderColor: 'divider' }}>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  Upload a new image
                </Typography>
                {uploadError && (
                  <Alert severity="error" sx={{ mb: 2 }}>
                    {uploadError}
                  </Alert>
                )}
                <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
                  <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" disabled={isUploading} />
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={isPrimaryImage}
                        onChange={(event) => setIsPrimaryImage(event.target.checked)}
                        disabled={isUploading}
                      />
                    }
                    label="Set as primary"
                  />
                  <Button
                    variant="contained"
                    size="small"
                    startIcon={isUploading ? <CircularProgress size={16} color="inherit" /> : <UploadIcon />}
                    onClick={() => void handleUpload()}
                    disabled={isUploading}
                  >
                    {isUploading ? 'Uploading...' : 'Upload'}
                  </Button>
                </Box>
              </Box>
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

          <Grid size={12}>
            <SellerDeedVerificationSection propertyId={propertyId} />
          </Grid>
        </Grid>
      )}

      <Dialog open={isWithdrawDialogOpen} onClose={() => setIsWithdrawDialogOpen(false)}>
        <DialogTitle>Withdraw this listing?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This removes the property from active review/browsing but keeps its verification and audit history.
          </DialogContentText>
          {withdrawError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {withdrawError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIsWithdrawDialogOpen(false)} disabled={isWithdrawing}>
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => void handleConfirmWithdraw()}
            disabled={isWithdrawing}
            startIcon={isWithdrawing ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            Withdraw Listing
          </Button>
        </DialogActions>
      </Dialog>
    </DashboardLayout>
  );
}
