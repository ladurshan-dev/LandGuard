import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Grid,
  IconButton,
  Paper,
  Tooltip,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { Link as RouterLink } from 'react-router-dom';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { RiskIndicator } from '../../../components/property/RiskIndicator';
import { getPropertiesBySeller, withdrawProperty } from '../../../services/propertyService';
import { formatCurrency, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyListingResult } from '../../../types/property';

/**
 * The seller's own listings - view, create, edit, withdraw. Deliberately
 * its own card layout (not the shared PropertyCard) because every row
 * here needs its own View/Edit/Withdraw actions, which PropertyCard's
 * single whole-card click target isn't built for; PropertyCard is reused
 * as-is for the buyer/admin browsing screens where a single click-through
 * is all that's needed.
 *
 * Withdrawn listings (Phase F - Property Withdrawal / Soft Delete) stay
 * visible here with a Withdrawn status chip rather than disappearing, per
 * the same "seller can still see it, just not manage it" pattern the rest
 * of this page already uses for read-only history - Edit/Withdraw actions
 * are hidden for a card that's already Withdrawn.
 */
export default function SellerPropertiesPage() {
  const { user } = useAuth();

  const [listings, setListings] = useState<PropertyListingResult[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pendingWithdrawId, setPendingWithdrawId] = useState<number | null>(null);
  const [isWithdrawing, setIsWithdrawing] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  // Promise-chained rather than async/await on purpose: the "no setState
  // synchronously reachable from an effect" compiler lint rule treats an
  // async function's whole body as reachable from the effect that calls
  // it, await boundaries or not, so an async fetch-then-setState function
  // still trips it the same as calling setState directly in the effect.
  // Only the .then/.catch/.finally callbacks below actually touch state,
  // and those are genuinely deferred until the request settles.
  const loadListings = useCallback((sellerId: number) => {
    getPropertiesBySeller(sellerId)
      .then((result) => {
        setListings(result);
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
    if (user) {
      loadListings(user.userId);
    }
  }, [user, loadListings]);

  if (!user) {
    return null;
  }

  const handleConfirmWithdraw = async () => {
    if (pendingWithdrawId === null) {
      return;
    }

    setIsWithdrawing(true);
    setWithdrawError(null);

    try {
      await withdrawProperty(pendingWithdrawId);
      // Re-fetch from the backend rather than patching the card locally -
      // this is the same "don't fake it client-side" rule
      // SellerPropertyDetailsPage's withdraw flow follows, and it means the
      // card picks up its real Withdrawn status (and anything else the
      // procedure changed) instead of a guessed partial update.
      loadListings(user.userId);
      setPendingWithdrawId(null);
    } catch (error) {
      setWithdrawError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsWithdrawing(false);
    }
  };

  return (
    <DashboardLayout title="My Properties" user={user} maxWidth="lg">
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" component="h1">
          My Properties
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          component={RouterLink}
          to="/seller/properties/new"
        >
          List a Property
        </Button>
      </Box>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && listings.length === 0 && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            You haven't listed any properties yet.
          </Typography>
          <Button
            variant="outlined"
            startIcon={<AddIcon />}
            component={RouterLink}
            to="/seller/properties/new"
            sx={{ mt: 2 }}
          >
            List your first property
          </Button>
        </Paper>
      )}

      {!isLoading && !loadError && listings.length > 0 && (
        <Grid container spacing={2}>
          {listings.map((listing) => (
            <Grid key={listing.propertyId} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 1 }}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                      {listing.title}
                    </Typography>
                    <PropertyStatusChip status={listing.status} />
                  </Box>

                  <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                    {listing.location}
                    {listing.district ? `, ${listing.district}` : ''} &middot; {formatSize(listing.size)}
                  </Typography>

                  <Typography variant="h6" color="primary" sx={{ fontWeight: 700, mt: 1 }}>
                    {formatCurrency(listing.price)}
                  </Typography>

                  <Box sx={{ mt: 1 }}>
                    <RiskIndicator
                      riskLevel={listing.riskLevel}
                      fraudStatus={listing.fraudStatus}
                      riskScore={listing.riskScore}
                    />
                  </Box>

                  <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 0.5, mt: 1.5 }}>
                    <Tooltip title="View details">
                      <IconButton
                        component={RouterLink}
                        to={`/seller/properties/${listing.propertyId}`}
                        size="small"
                      >
                        <VisibilityIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    {listing.status !== 'Withdrawn' && (
                      <Tooltip title="Edit">
                        <IconButton
                          component={RouterLink}
                          to={`/seller/properties/${listing.propertyId}/edit`}
                          size="small"
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    {listing.status !== 'Withdrawn' && (
                      <Tooltip title="Withdraw listing">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => {
                            setWithdrawError(null);
                            setPendingWithdrawId(listing.propertyId);
                          }}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </Box>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Dialog open={pendingWithdrawId !== null} onClose={() => setPendingWithdrawId(null)}>
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
          <Button onClick={() => setPendingWithdrawId(null)} disabled={isWithdrawing}>
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
