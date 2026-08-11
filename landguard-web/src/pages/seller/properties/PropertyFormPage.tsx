import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Grid,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { SellerIdentityStatusBanner } from '../../../components/seller/SellerIdentityStatusBanner';
import { DeedDocumentUpload } from '../../../components/property/DeedDocumentUpload';
import { createProperty, getPropertyById, updateProperty } from '../../../services/propertyService';
import { verifyDeed } from '../../../services/deedVerificationService';
import { ApiError } from '../../../utils/apiError';

/**
 * PropertyValidationRules mirrored from the backend (see
 * LandGuard.Application.DTOs.Property.PropertyValidationRules) purely for
 * client-side validation - the backend re-validates everything itself
 * regardless, this only gives the seller faster feedback than a round
 * trip.
 */
const TITLE_MAX_LENGTH = 200;
const LOCATION_MAX_LENGTH = 255;
const DISTRICT_MAX_LENGTH = 100;
const DEED_REFERENCE_MAX_LENGTH = 100;
const DESCRIPTION_MAX_LENGTH = 4000;
const OWNER_NAME_MAX_LENGTH = 150;
const OWNER_ADDRESS_MAX_LENGTH = 255;
/** Mirrors the backend's AuthValidationRules.NicPattern - old format: 9 digits + V/X; new format: 12 digits. */
const OWNER_NIC_PATTERN = /^([0-9]{9}[VvXx]|[0-9]{12})$/;

interface PropertyFormValues {
  title: string;
  description: string;
  location: string;
  district: string;
  latitude: string;
  longitude: string;
  size: string;
  price: string;
  deedReference: string;
  ownerName: string;
  ownerNic: string;
  ownerAddress: string;
  regeocodeLocation: boolean;
}

const EMPTY_DEFAULTS: PropertyFormValues = {
  title: '',
  description: '',
  location: '',
  district: '',
  latitude: '',
  longitude: '',
  size: '',
  price: '',
  deedReference: '',
  ownerName: '',
  ownerNic: '',
  ownerAddress: '',
  regeocodeLocation: false,
};

/**
 * Create and edit share one form (same fields, same validation, only the
 * submit call and the initial values differ) rather than two
 * near-duplicate pages - CreatePropertyRequest and UpdatePropertyRequest
 * are the same shape besides regeocodeLocation/optionality, so a second
 * copy of this form would just be drift waiting to happen.
 */
export default function PropertyFormPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEditMode = id !== undefined;
  const propertyId = id ? Number(id) : null;

  const [isLoadingExisting, setIsLoadingExisting] = useState(isEditMode);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Deed upload (Phase D) - CREATE mode only. EDIT mode deliberately has no
  // deed upload here at all: this project has no way yet to know whether a
  // property already has a verified deed on file, so re-requiring one on
  // every edit would either force a pointless re-upload or silently
  // overwrite a perfectly good past verification - neither is safe to
  // invent. An optional "Replace / Re-verify Deed" action belongs on
  // SellerPropertyDetailsPage instead, where the seller can already see
  // whether a verification exists before deciding to replace it.
  const [selectedDeedFile, setSelectedDeedFile] = useState<File | null>(null);
  const [deedFileError, setDeedFileError] = useState<string | null>(null);
  const [submitPhase, setSubmitPhase] = useState<'idle' | 'creating' | 'verifying'>('idle');

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<PropertyFormValues>({ defaultValues: EMPTY_DEFAULTS });

  useEffect(() => {
    if (!isEditMode || propertyId === null || !user) {
      return;
    }

    let cancelled = false;

    // isLoadingExisting/loadError already start correctly ("loading" only
    // in edit mode, no error) via their initial state above - no setState
    // runs synchronously before the first `await` here, matching the same
    // constraint SellerPropertiesPage/SellerPropertyDetailsPage apply.
    (async () => {
      try {
        const detail = await getPropertyById(propertyId);

        if (detail.listing.sellerId !== user.userId) {
          // Mirrors the backend's own account-enumeration-safe behaviour:
          // a non-owner gets the same "not found" wording as a genuinely
          // missing id, never a distinct "not yours" message.
          if (!cancelled) {
            setLoadError('Property not found.');
          }
          return;
        }

        if (!cancelled) {
          reset({
            title: detail.listing.title,
            description: detail.listing.description ?? '',
            location: detail.listing.location,
            district: detail.listing.district ?? '',
            latitude: detail.listing.latitude?.toString() ?? '',
            longitude: detail.listing.longitude?.toString() ?? '',
            size: detail.listing.size.toString(),
            price: detail.listing.price.toString(),
            deedReference: detail.listing.deedReference ?? '',
            ownerName: detail.listing.ownerName ?? '',
            ownerNic: detail.listing.ownerNic ?? '',
            ownerAddress: detail.listing.ownerAddress ?? '',
            regeocodeLocation: false,
          });
        }
      } catch (error) {
        if (!cancelled) {
          setLoadError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
        }
      } finally {
        if (!cancelled) {
          setIsLoadingExisting(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isEditMode, propertyId, user, reset]);

  if (!user) {
    return null;
  }

  // Seller Government Identity Verification requirement: blocks the
  // CREATE form outright for a Pending/Failed Seller, rather than letting
  // them fill out the whole form only to have the submit rejected server-
  // side. EDIT mode is untouched - identity verification only gates
  // creating a NEW listing, per this requirement's own scope.
  if (!isEditMode && user.identityStatus !== 'Verified') {
    return (
      <DashboardLayout title="List a Property" user={user}>
        <SellerIdentityStatusBanner />
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            Your identity must be verified before you can list a property.
          </Typography>
          <Button variant="outlined" onClick={() => navigate('/seller/properties')} sx={{ mt: 2 }}>
            Back to My Properties
          </Button>
        </Paper>
      </DashboardLayout>
    );
  }

  const onSubmit = async (values: PropertyFormValues) => {
    setSubmitError(null);
    setDeedFileError(null);

    const trimmedDescription = values.description.trim();
    const trimmedDistrict = values.district.trim();
    const trimmedDeedReference = values.deedReference.trim();
    const trimmedOwnerName = values.ownerName.trim();
    const trimmedOwnerNic = values.ownerNic.trim();
    const trimmedOwnerAddress = values.ownerAddress.trim();
    const latitude = values.latitude.trim() === '' ? undefined : Number(values.latitude);
    const longitude = values.longitude.trim() === '' ? undefined : Number(values.longitude);

    if (!isEditMode && !selectedDeedFile) {
      setDeedFileError('Upload the deed document that proves ownership of this property before listing it.');
      return;
    }

    try {
      if (isEditMode && propertyId !== null) {
        // Navigate using the id already known from the route, not
        // `updated.propertyId` - PUT /api/properties/{id}'s response body
        // has come back with propertyId 0 in practice (see PropertyFormPage
        // bug report: editing property 31 landed on /seller/properties/0),
        // and the route's own `propertyId` is already confirmed correct
        // (this branch only runs in edit mode, where it came from the URL
        // we're already editing). No backend change needed for this fix.
        await updateProperty(propertyId, {
          title: values.title.trim(),
          description: trimmedDescription === '' ? undefined : trimmedDescription,
          location: values.location.trim(),
          district: trimmedDistrict === '' ? undefined : trimmedDistrict,
          latitude,
          longitude,
          size: Number(values.size),
          price: Number(values.price),
          deedReference: trimmedDeedReference === '' ? undefined : trimmedDeedReference,
          ownerName: trimmedOwnerName === '' ? undefined : trimmedOwnerName,
          ownerNic: trimmedOwnerNic === '' ? undefined : trimmedOwnerNic,
          ownerAddress: trimmedOwnerAddress === '' ? undefined : trimmedOwnerAddress,
          regeocodeLocation: values.regeocodeLocation,
        });
        navigate(`/seller/properties/${propertyId}`);
      } else {
        // Two-step orchestration (Phase D): the existing property-create
        // API stays JSON-only (CreatePropertyRequest is unchanged), so the
        // deed is uploaded as a second request once the real PropertyID is
        // known, using the existing POST /api/deed-verification/{id}
        // multipart endpoint - not a new combined multipart create
        // endpoint. The property is created first and is Pending either
        // way; nothing below can undo that.
        setSubmitPhase('creating');
        const created = await createProperty({
          title: values.title.trim(),
          description: trimmedDescription === '' ? undefined : trimmedDescription,
          location: values.location.trim(),
          district: trimmedDistrict === '' ? undefined : trimmedDistrict,
          latitude,
          longitude,
          size: Number(values.size),
          price: Number(values.price),
          deedReference: trimmedDeedReference,
          ownerName: trimmedOwnerName,
          ownerNic: trimmedOwnerNic,
          ownerAddress: trimmedOwnerAddress,
        });

        setSubmitPhase('verifying');
        try {
          await verifyDeed(created.propertyId, selectedDeedFile!);
          navigate(`/seller/properties/${created.propertyId}`);
        } catch {
          // The property (e.g. #34) already exists and stays Pending
          // regardless of this failure - never deleted, never approved or
          // rejected automatically, and never navigated to /0. The seller
          // lands on their new property's own details page instead, which
          // reports the failure and offers a Retry Deed Verification
          // action (see SellerPropertyDetailsPage's handling of this
          // navigation state) rather than silently losing the failure here.
          navigate(`/seller/properties/${created.propertyId}`, {
            state: { deedVerificationFailed: true },
          });
        }
      }
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setSubmitPhase('idle');
    }
  };

  const submitButtonLabel = isSubmitting
    ? submitPhase === 'verifying'
      ? 'Verifying deed...'
      : isEditMode
        ? 'Saving...'
        : 'Creating property...'
    : isEditMode
      ? 'Save Changes'
      : 'List Property';

  return (
    <DashboardLayout title={isEditMode ? 'Edit Property' : 'List a Property'} user={user} maxWidth="md">
      <Typography variant="h5" component="h1" sx={{ mb: 3 }}>
        {isEditMode ? 'Edit Property' : 'List a Property'}
      </Typography>

      {isLoadingExisting && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoadingExisting && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoadingExisting && !loadError && (
        <Paper variant="outlined" sx={{ p: 3 }}>
          {submitError && (
            <Alert severity="error" role="alert" sx={{ mb: 2 }}>
              {submitError}
            </Alert>
          )}

          <Box component="form" noValidate onSubmit={handleSubmit(onSubmit)}>
            <Grid container spacing={2}>
              <Grid size={12}>
                <TextField
                  {...register('title', {
                    required: 'Title is required.',
                    maxLength: { value: TITLE_MAX_LENGTH, message: `Title must be at most ${TITLE_MAX_LENGTH} characters.` },
                  })}
                  label="Title"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.title)}
                  helperText={errors.title?.message}
                />
              </Grid>

              <Grid size={12}>
                <TextField
                  {...register('description', {
                    maxLength: {
                      value: DESCRIPTION_MAX_LENGTH,
                      message: `Description must be at most ${DESCRIPTION_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Description"
                  fullWidth
                  multiline
                  minRows={3}
                  disabled={isSubmitting}
                  error={Boolean(errors.description)}
                  helperText={errors.description?.message}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 8 }}>
                <TextField
                  {...register('location', {
                    required: 'Location is required.',
                    maxLength: {
                      value: LOCATION_MAX_LENGTH,
                      message: `Location must be at most ${LOCATION_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Location"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.location)}
                  helperText={errors.location?.message}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  {...register('district', {
                    maxLength: {
                      value: DISTRICT_MAX_LENGTH,
                      message: `District must be at most ${DISTRICT_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="District"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.district)}
                  helperText={errors.district?.message}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('size', {
                    required: 'Size is required.',
                    validate: (value) => Number(value) > 0 || 'Size must be a positive number.',
                  })}
                  label="Size (perches)"
                  type="number"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.size)}
                  helperText={errors.size?.message}
                  slotProps={{ htmlInput: { step: 'any', min: 0 } }}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('price', {
                    required: 'Price is required.',
                    validate: (value) => Number(value) > 0 || 'Price must be a positive number.',
                  })}
                  label="Price (LKR)"
                  type="number"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.price)}
                  helperText={errors.price?.message}
                  slotProps={{ htmlInput: { step: 'any', min: 0 } }}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('latitude')}
                  label="Latitude (optional)"
                  type="number"
                  fullWidth
                  disabled={isSubmitting}
                  helperText="Leave blank to auto-locate from the address."
                  slotProps={{ htmlInput: { step: 'any' } }}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('longitude')}
                  label="Longitude (optional)"
                  type="number"
                  fullWidth
                  disabled={isSubmitting}
                  slotProps={{ htmlInput: { step: 'any' } }}
                />
              </Grid>

              <Grid size={12}>
                <TextField
                  {...register('deedReference', {
                    required: 'Deed Number is required.',
                    maxLength: {
                      value: DEED_REFERENCE_MAX_LENGTH,
                      message: `Deed Number must be at most ${DEED_REFERENCE_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Deed Number"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.deedReference)}
                  helperText={errors.deedReference?.message}
                />
              </Grid>

              <Grid size={12}>
                <Typography variant="subtitle2" sx={{ mt: 1 }}>
                  Deed Owner Details
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  The registered owner information on the deed - this is checked against the deed document you
                  upload, and must match it exactly.
                </Typography>
              </Grid>

              <Grid size={12}>
                <TextField
                  {...register('ownerName', {
                    required: 'Owner Name is required.',
                    maxLength: {
                      value: OWNER_NAME_MAX_LENGTH,
                      message: `Owner Name must be at most ${OWNER_NAME_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Owner Name"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.ownerName)}
                  helperText={errors.ownerName?.message}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('ownerNic', {
                    required: 'Owner NIC is required.',
                    pattern: {
                      value: OWNER_NIC_PATTERN,
                      message: 'Enter a valid Sri Lankan NIC (9 digits followed by V or X, or 12 digits).',
                    },
                  })}
                  label="Owner NIC"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.ownerNic)}
                  helperText={errors.ownerNic?.message}
                />
              </Grid>

              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  {...register('ownerAddress', {
                    required: 'Owner Address is required.',
                    maxLength: {
                      value: OWNER_ADDRESS_MAX_LENGTH,
                      message: `Owner Address must be at most ${OWNER_ADDRESS_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Owner Address"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.ownerAddress)}
                  helperText={errors.ownerAddress?.message}
                />
              </Grid>

              {!isEditMode && (
                <Grid size={12}>
                  <DeedDocumentUpload
                    selectedFile={selectedDeedFile}
                    onFileSelected={(file) => {
                      setSelectedDeedFile(file);
                      setDeedFileError(null);
                    }}
                    onRemove={() => setSelectedDeedFile(null)}
                    disabled={isSubmitting}
                    error={deedFileError}
                  />
                </Grid>
              )}

              {isEditMode && (
                <Grid size={12}>
                  <FormControlLabel
                    control={<Checkbox {...register('regeocodeLocation')} disabled={isSubmitting} />}
                    label="Re-calculate map coordinates from the location/district above"
                  />
                </Grid>
              )}
            </Grid>

            <Box sx={{ display: 'flex', gap: 2, mt: 3 }}>
              <Button
                type="submit"
                variant="contained"
                disabled={isSubmitting}
                startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
              >
                {submitButtonLabel}
              </Button>
              <Button variant="text" disabled={isSubmitting} onClick={() => navigate(-1)}>
                Cancel
              </Button>
            </Box>
          </Box>
        </Paper>
      )}
    </DashboardLayout>
  );
}
