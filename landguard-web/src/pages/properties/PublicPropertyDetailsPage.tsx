import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Container, Grid, Paper, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PlaceIcon from '@mui/icons-material/Place';
import VerifiedIcon from '@mui/icons-material/Verified';
import GppGoodIcon from '@mui/icons-material/GppGood';
import LoginIcon from '@mui/icons-material/Login';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import { PublicLayout } from '../../layouts/PublicLayout';
import { useAuth } from '../../hooks/useAuth';
import { PropertyImageGallery } from '../../components/property/PropertyImageGallery';
import { getPropertyById } from '../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../utils/format';
import { ApiError } from '../../utils/apiError';
import type { PropertyDetail } from '../../types/property';

/**
 * Fully public, read-only property detail page - reuses the same
 * AllowAnonymous GET /api/properties/{id} (getPropertyById)
 * BuyerPropertyDetailsPage already uses.
 *
 * Presentation-layer hardening: PropertyService.GetByIdAsync is designed
 * for BOTH public and internal callers. For a Buyer/anonymous caller it
 * only ever returns a currently-Approved property, and redacts
 * ownerName/ownerNic/ownerAddress/deedReference/sellerPhone/riskScore/
 * riskLevel/fraudStatus/riskSummary/riskGeneratedDate/fraudReport - but for
 * the property's own owner (a Seller) or an Admin, it intentionally
 * returns a Pending/Flagged/Rejected/Disapproved/Withdrawn listing too,
 * fully unredacted - that's correct for the protected Seller/Admin detail
 * pages. axios attaches the JWT automatically whenever one exists (see
 * api/axios.ts), so a signed-in Seller/Admin who manually navigates to
 * this PUBLIC route would otherwise see their own non-Approved listing
 * here, in a context meant for anyone. Public routes must only ever
 * display Approved properties, full stop - so this page adds its own
 * status check on the client and refuses to render anything from
 * `detail.listing`/`detail.images` unless status is exactly "Approved",
 * falling back to a plain "not publicly available" state instead. This is
 * a presentation-layer guard only, specific to this public route; it does
 * not change, weaken or duplicate the backend's own authorization logic,
 * and the protected Seller/Admin/Buyer detail pages are untouched and keep
 * seeing every status exactly as before.
 *
 * No "Contact Seller" action is reimplemented here (that workflow is
 * Buyer-only, backed by a RequireBuyer-policy endpoint):
 * - Not signed in: "Sign In to Contact the Seller" -> /login. No seller
 *   contact detail is ever fetched or shown to a guest.
 * - Signed in as Buyer: sent to the real, existing protected page
 *   ("/buyer/properties/:id") where that action actually lives - nothing
 *   duplicated here.
 * - Signed in as Seller or Admin: no contact action is rendered at all.
 *   Neither role can use the Buyer-only endpoint, and telling an
 *   already-authenticated user to "sign in" would be misleading, not just
 *   unhelpful.
 *
 * No "Save Property" control either - no such feature exists anywhere in
 * this codebase's frontend to reuse (confirmed during Phase 1
 * investigation), so nothing is invented here to fill that gap.
 */
export default function PublicPropertyDetailsPage() {
  const { isAuthenticated, user } = useAuth();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

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

  if (propertyId === null) {
    return null;
  }

  // Public-route hardening: even though the backend legitimately returned
  // this row (e.g. to its owning Seller or an Admin browsing their own
  // JWT-authenticated session), this specific route must never render a
  // non-Approved property's information, seller information, or
  // verification/risk information. Checked once here, up front, so every
  // render branch below it can assume "detail present" means "Approved and
  // safe to show publicly".
  const isPublicallyVisible = detail !== null && detail.listing.status === 'Approved';

  return (
    <PublicLayout>
      <Container maxWidth="md" sx={{ py: { xs: 3, md: 5 } }}>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to="/properties" sx={{ mb: 2 }}>
          Back to Browse
        </Button>

        {isLoading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress />
          </Box>
        )}

        {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

        {!isLoading && !loadError && detail && !isPublicallyVisible && (
          <Paper variant="outlined" sx={{ p: 5, textAlign: 'center' }}>
            <VisibilityOffIcon sx={{ fontSize: 40, color: 'text.disabled', mb: 1.5 }} />
            <Typography variant="h6" sx={{ mb: 1 }}>
              Property unavailable
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 3 }}>
              This property is not publicly available.
            </Typography>
            <Button variant="contained" component={RouterLink} to="/properties">
              Back to Browse
            </Button>
          </Paper>
        )}

        {!isLoading && !loadError && isPublicallyVisible && detail && (
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
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Typography variant="caption" color="text.secondary">Price</Typography>
                    <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatCurrency(detail.listing.price)}</Typography>
                  </Grid>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Typography variant="caption" color="text.secondary">Size</Typography>
                    <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatSize(detail.listing.size)}</Typography>
                  </Grid>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Typography variant="caption" color="text.secondary">Listed On</Typography>
                    <Typography variant="body1" sx={{ fontWeight: 600 }}>{formatDate(detail.listing.uploadDate)}</Typography>
                  </Grid>
                </Grid>

                <Alert icon={<GppGoodIcon fontSize="inherit" />} severity="success" variant="outlined" sx={{ mt: 2.5 }}>
                  This listing's deed has been compared against government registry records and was manually
                  approved by a LandGuard administrator.
                </Alert>

                <Box sx={{ mt: 2, pt: 2, borderTop: 1, borderColor: 'divider' }}>
                  <Typography variant="subtitle2" color="text.secondary">Seller</Typography>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                    <Typography variant="body1">{detail.listing.sellerName}</Typography>
                    {detail.listing.sellerNicVerified && (
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25, color: 'success.main' }}>
                        <VerifiedIcon fontSize="small" color="success" />
                        <Typography variant="body2" color="success.main">Verified Seller</Typography>
                      </Box>
                    )}
                  </Box>

                  {/*
                    Contact Seller - see this file's own top doc comment for
                    the full three-way reasoning (guest / Buyer / Seller-
                    Admin). Guest and Buyer each get one clear action;
                    an authenticated Seller or Admin gets none, rather than
                    a button that would be misleading (telling someone
                    already signed in to "sign in") or a dead end (a
                    Buyer-only endpoint they cannot use).
                  */}
                  {!isAuthenticated && (
                    <Button
                      variant="outlined"
                      size="small"
                      startIcon={<LoginIcon />}
                      sx={{ mt: 1.5 }}
                      component={RouterLink}
                      to="/login"
                    >
                      Sign In to Contact the Seller
                    </Button>
                  )}

                  {isAuthenticated && user?.role === 'Buyer' && (
                    <Button
                      variant="outlined"
                      size="small"
                      sx={{ mt: 1.5 }}
                      onClick={() => navigate(`/buyer/properties/${propertyId}`)}
                    >
                      Contact Seller
                    </Button>
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
          </Grid>
        )}
      </Container>
    </PublicLayout>
  );
}
