import { useCallback, useEffect, useRef, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
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
import { deleteProperty, deletePropertyImage, getPropertyById, uploadPropertyImage } from '../../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail } from '../../../types/property';

/** The seller's view of one of their own properties: full detail, images, fraud report, plus edit/delete/upload actions. */
export default function SellerPropertyDetailsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

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

  const handleConfirmDelete = async () => {
    setIsDeleting(true);
    setDeleteError(null);

    try {
      await deleteProperty(propertyId);
      navigate('/seller/properties', { replace: true });
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      setIsDeleting(false);
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
                <Button
                  variant="outlined"
                  startIcon={<EditIcon />}
                  component={RouterLink}
                  to={`/seller/properties/${propertyId}/edit`}
                >
                  Edit
                </Button>
                <Button
                  variant="outlined"
                  color="error"
                  startIcon={<DeleteIcon />}
                  onClick={() => {
                    setDeleteError(null);
                    setIsDeleteDialogOpen(true);
                  }}
                >
                  Delete
                </Button>
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
