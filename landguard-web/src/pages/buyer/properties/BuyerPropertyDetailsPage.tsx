import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Grid, Link, Paper, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PlaceIcon from '@mui/icons-material/Place';
import PhoneIcon from '@mui/icons-material/Phone';
import EmailIcon from '@mui/icons-material/Email';
import VerifiedIcon from '@mui/icons-material/Verified';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyImageGallery } from '../../../components/property/PropertyImageGallery';
import { getPropertyById, getSellerContact } from '../../../services/propertyService';
import { formatCurrency, formatDate, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertyDetail, SellerContactInfo } from '../../../types/property';

/**
 * A buyer's read-only view of one property. No edit/delete/status
 * controls - a buyer can only ever reach an Approved listing here anyway
 * (PropertyService.GetByIdAsync returns the same "Property not found" for
 * a non-Approved id as for a nonexistent one unless the caller is the
 * owner or an Admin), so this page never needs its own visibility check.
 *
 * Buyer privacy requirement: no fraud/risk panel here, deliberately -
 * Approval is sufficient information for a Buyer. The backend also now
 * redacts riskScore/riskLevel/fraudStatus/riskSummary/riskGeneratedDate,
 * deedReference, ownerName/ownerNic/ownerAddress and sellerPhone on
 * detail.listing, and returns an empty fraudReport, for this exact caller
 * (see PropertyService.GetByIdAsync/RedactOwnerFields/
 * RedactSellerContactFields on the backend) - so this isn't just a UI
 * omission, the data is genuinely not in the response for a Buyer to begin
 * with. Deed Reference is therefore not rendered anywhere on this page.
 *
 * Contact Seller workflow: the Seller's phone/email are never part of
 * `detail` at all for a Buyer - they are only ever fetched, on demand, via
 * `getSellerContact` when the Buyer explicitly clicks "Contact Seller"
 * below, and are kept only in this component's own React state (never
 * written to localStorage/sessionStorage, and cleared if the Buyer
 * navigates to a different property).
 */
export default function BuyerPropertyDetailsPage() {
  const { user } = useAuth();
  const { id } = useParams<{ id: string }>();
  const propertyId = id ? Number(id) : null;

  const [detail, setDetail] = useState<PropertyDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [contact, setContact] = useState<SellerContactInfo | null>(null);
  const [isContactLoading, setIsContactLoading] = useState(false);
  const [contactError, setContactError] = useState<string | null>(null);
  // Tracks which propertyId `contact`/`contactError` belong to, purely so the
  // block below can detect "the id changed" and reset them.
  const [contactLoadedFor, setContactLoadedFor] = useState<number | null>(null);

  // Adjusting state when a prop changes, during render rather than in an
  // effect (https://react.dev/learn/you-might-not-need-an-effect#adjusting-some-state-when-a-prop-changes) -
  // never carry Seller A's fetched phone/email over to Seller B's page when
  // navigating between properties without a full remount.
  if (propertyId !== contactLoadedFor) {
    setContactLoadedFor(propertyId);
    setContact(null);
    setContactError(null);
  }

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

  const handleContactSeller = useCallback(() => {
    if (propertyId === null) {
      return;
    }

    setIsContactLoading(true);
    setContactError(null);

    getSellerContact(propertyId)
      .then((result) => {
        setContact(result);
      })
      .catch((error: unknown) => {
        setContactError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      })
      .finally(() => {
        setIsContactLoading(false);
      });
  }, [propertyId]);

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

              {/*
                Contact Seller workflow. Before any click: name + a simple
                Verified Seller badge only, both already present, unredacted,
                on detail.listing - no phone/email rendered anywhere here.
                After a successful click: the fetched SellerContactInfo
                (component state only, never persisted) renders phone/email
                as tel:/mailto: links. LandGuard does not process payments or
                legal ownership transfer, so this is deliberately "Contact
                Seller", never "Buy Property".
              */}
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

                {!contact && (
                  <Button
                    variant="outlined"
                    size="small"
                    sx={{ mt: 1.5 }}
                    onClick={handleContactSeller}
                    disabled={isContactLoading}
                    startIcon={isContactLoading ? <CircularProgress size={16} /> : undefined}
                  >
                    {isContactLoading ? 'Requesting...' : 'Contact Seller'}
                  </Button>
                )}

                {contactError && (
                  <Alert severity="error" sx={{ mt: 1.5 }}>{contactError}</Alert>
                )}

                {contact && (
                  <Box sx={{ mt: 1.5 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Seller Contact Information</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Name</Typography>
                    <Typography variant="body1">{contact.sellerName}</Typography>

                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Phone</Typography>
                    {contact.phone ? (
                      <Link href={`tel:${contact.phone}`} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <PhoneIcon fontSize="small" />
                        {contact.phone}
                      </Link>
                    ) : (
                      <Typography variant="body1" color="text.secondary">Not provided</Typography>
                    )}

                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Email</Typography>
                    {contact.email ? (
                      <Link href={`mailto:${contact.email}`} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <EmailIcon fontSize="small" />
                        {contact.email}
                      </Link>
                    ) : (
                      <Typography variant="body1" color="text.secondary">Not provided</Typography>
                    )}
                  </Box>
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
    </DashboardLayout>
  );
}
