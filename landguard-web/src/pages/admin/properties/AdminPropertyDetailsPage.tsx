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
  TextField,
  Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import DeleteIcon from '@mui/icons-material/Delete';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { PropertyImageGallery } from '../../../components/property/PropertyImageGallery';
import { PropertyFraudPanel } from '../../../components/property/PropertyFraudPanel';
import { DeedVerificationPanel } from '../../../components/property/DeedVerificationPanel';
import { deleteProperty, getPropertyById } from '../../../services/propertyService';
import { approveProperty, rejectProperty } from '../../../services/adminService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail } from '../../../types/property';

/**
 * Admin's review screen for any single property, regardless of status -
 * the one place this app's Admin role can reach a Pending/Flagged/
 * Rejected listing, since PropertyService.GetByIdAsync grants an Admin
 * caller visibility of any status while GET /api/properties (search) does
 * not. Organized into the sections this phase's review workflow needs:
 * property info, seller info, fraud/risk (supporting indicators only -
 * PropertyFraudPanel, explicitly labeled as such below), Government Deed
 * Verification (DeedVerificationPanel, kept visually and logically
 * separate - authoritative evidence, not a supporting indicator), and
 * Admin Decision (Approve/Reject, shown only while Status = Pending -
 * matching the Phase C workflow where Approved/Rejected are terminal,
 * admin-only transitions with no "undo" endpoint on the backend to call).
 * Delete remains available regardless of status, unchanged from before.
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

  const [isApproveDialogOpen, setIsApproveDialogOpen] = useState(false);
  const [approveRemarks, setApproveRemarks] = useState('');
  const [isApproving, setIsApproving] = useState(false);
  const [approveError, setApproveError] = useState<string | null>(null);

  const [isRejectDialogOpen, setIsRejectDialogOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [isRejecting, setIsRejecting] = useState(false);
  const [rejectError, setRejectError] = useState<string | null>(null);

  const [moderationNotice, setModerationNotice] = useState<string | null>(null);

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

  const handleApprove = async () => {
    setIsApproving(true);
    setApproveError(null);

    try {
      const trimmedRemarks = approveRemarks.trim();
      await approveProperty(propertyId, trimmedRemarks === '' ? {} : { remarks: trimmedRemarks });
      setIsApproveDialogOpen(false);
      setApproveRemarks('');
      setModerationNotice('This property has been approved.');
      setIsLoading(true);
      loadDetail(propertyId);
    } catch (error) {
      setApproveError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsApproving(false);
    }
  };

  const handleReject = async () => {
    const trimmedReason = rejectReason.trim();
    if (trimmedReason === '') {
      setRejectError('A rejection reason is required.');
      return;
    }

    setIsRejecting(true);
    setRejectError(null);

    try {
      await rejectProperty(propertyId, { reason: trimmedReason });
      setIsRejectDialogOpen(false);
      setRejectReason('');
      setModerationNotice('This property has been rejected.');
      setIsLoading(true);
      loadDetail(propertyId);
    } catch (error) {
      setRejectError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsRejecting(false);
    }
  };

  return (
    <DashboardLayout title="Property Details" user={user} maxWidth="md">
      <Button startIcon={<ArrowBackIcon />} component={RouterLink} to="/admin/properties" sx={{ mb: 2 }}>
        Back to Property Oversight
      </Button>

      {moderationNotice && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setModerationNotice(null)}>
          {moderationNotice}
        </Alert>
      )}

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
              <Typography variant="overline" color="text.secondary">
                A. Property Information
              </Typography>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2, flexWrap: 'wrap', mt: 0.5 }}>
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
                <Typography variant="overline" color="text.secondary">
                  B. Seller Information
                </Typography>
                <Typography variant="body1" sx={{ mt: 0.5 }}>
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
            <Typography variant="overline" color="text.secondary">
              C. Supporting Risk Indicators
            </Typography>
            <Box sx={{ mt: 0.5 }}>
              <PropertyFraudPanel
                riskLevel={detail.listing.riskLevel}
                fraudStatus={detail.listing.fraudStatus}
                riskScore={detail.listing.riskScore}
                riskSummary={detail.listing.riskSummary}
                riskGeneratedDate={detail.listing.riskGeneratedDate}
                fraudReport={detail.fraudReport}
                showRulePoints
              />
            </Box>
          </Grid>

          <Grid size={12}>
            <Typography variant="overline" color="text.secondary">
              D. Government Deed Verification
            </Typography>
            <Box sx={{ mt: 0.5 }}>
              <DeedVerificationPanel propertyId={propertyId} />
            </Box>
          </Grid>

          <Grid size={12}>
            <Typography variant="overline" color="text.secondary">
              E. Admin Decision
            </Typography>
            <Paper variant="outlined" sx={{ p: 3, mt: 0.5 }}>
              {detail.listing.status === 'Pending' ? (
                <>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Review the property information, risk indicators and deed verification above, then approve or
                    reject this listing. This action is recorded and notifies the seller; it cannot be undone from
                    this screen.
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
                    <Button
                      variant="contained"
                      color="success"
                      startIcon={<CheckCircleIcon />}
                      onClick={() => {
                        setApproveError(null);
                        setIsApproveDialogOpen(true);
                      }}
                    >
                      Approve Property
                    </Button>
                    <Button
                      variant="outlined"
                      color="error"
                      startIcon={<CancelIcon />}
                      onClick={() => {
                        setRejectError(null);
                        setIsRejectDialogOpen(true);
                      }}
                    >
                      Reject Property
                    </Button>
                  </Box>
                </>
              ) : (
                <Typography color="text.secondary">
                  This property has already been decided (Status: {detail.listing.status}). No further moderation
                  action is available here.
                </Typography>
              )}
            </Paper>
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

      <Dialog
        open={isApproveDialogOpen}
        onClose={() => {
          if (!isApproving) {
            setIsApproveDialogOpen(false);
          }
        }}
      >
        <DialogTitle>Approve this property?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This sets the listing's status to Approved and notifies the seller. This cannot be undone from this
            screen.
          </DialogContentText>
          <TextField
            label="Remarks (optional)"
            fullWidth
            multiline
            minRows={2}
            sx={{ mt: 2 }}
            value={approveRemarks}
            onChange={(event) => setApproveRemarks(event.target.value)}
            disabled={isApproving}
          />
          {approveError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {approveError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIsApproveDialogOpen(false)} disabled={isApproving}>
            Cancel
          </Button>
          <Button
            color="success"
            variant="contained"
            onClick={() => void handleApprove()}
            disabled={isApproving}
            startIcon={isApproving ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            Approve
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={isRejectDialogOpen}
        onClose={() => {
          if (!isRejecting) {
            setIsRejectDialogOpen(false);
          }
        }}
      >
        <DialogTitle>Reject this property?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This sets the listing's status to Rejected and notifies the seller with your reason. This cannot be
            undone from this screen.
          </DialogContentText>
          <TextField
            label="Rejection reason"
            required
            fullWidth
            multiline
            minRows={2}
            sx={{ mt: 2 }}
            value={rejectReason}
            onChange={(event) => setRejectReason(event.target.value)}
            disabled={isRejecting}
            error={rejectError !== null}
          />
          {rejectError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {rejectError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIsRejectDialogOpen(false)} disabled={isRejecting}>
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => void handleReject()}
            disabled={isRejecting || rejectReason.trim() === ''}
            startIcon={isRejecting ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            Reject
          </Button>
        </DialogActions>
      </Dialog>
    </DashboardLayout>
  );
}
