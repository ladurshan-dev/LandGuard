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
import { createProperty, getPropertyById, updateProperty } from '../../../services/propertyService';
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

  const onSubmit = async (values: PropertyFormValues) => {
    setSubmitError(null);

    const trimmedDescription = values.description.trim();
    const trimmedDistrict = values.district.trim();
    const trimmedDeedReference = values.deedReference.trim();
    const latitude = values.latitude.trim() === '' ? undefined : Number(values.latitude);
    const longitude = values.longitude.trim() === '' ? undefined : Number(values.longitude);

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
          regeocodeLocation: values.regeocodeLocation,
        });
        navigate(`/seller/properties/${propertyId}`);
      } else {
        const created = await createProperty({
          title: values.title.trim(),
          description: trimmedDescription === '' ? undefined : trimmedDescription,
          location: values.location.trim(),
          district: trimmedDistrict === '' ? undefined : trimmedDistrict,
          latitude,
          longitude,
          size: Number(values.size),
          price: Number(values.price),
          deedReference: trimmedDeedReference === '' ? undefined : trimmedDeedReference,
        });
        navigate(`/seller/properties/${created.propertyId}`);
      }
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    }
  };

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
                    maxLength: {
                      value: DEED_REFERENCE_MAX_LENGTH,
                      message: `Deed reference must be at most ${DEED_REFERENCE_MAX_LENGTH} characters.`,
                    },
                  })}
                  label="Deed Reference (optional)"
                  fullWidth
                  disabled={isSubmitting}
                  error={Boolean(errors.deedReference)}
                  helperText={errors.deedReference?.message}
                />
              </Grid>

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
                {isSubmitting ? 'Saving...' : isEditMode ? 'Save Changes' : 'List Property'}
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
