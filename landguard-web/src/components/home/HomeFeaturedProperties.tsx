import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, Button, CircularProgress, Container, Grid, Paper, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import { PropertyCard } from '../property/PropertyCard';
import { searchProperties } from '../../services/propertyService';
import { ApiError } from '../../utils/apiError';
import type { PropertySearchResponse } from '../../types/property';

const FEATURED_COUNT = 3;

/**
 * Up to 3 real, currently-Approved properties, fetched via the same
 * AllowAnonymous GET /api/properties (searchProperties) every other public
 * search on this app already uses - no separate/fabricated "featured"
 * endpoint or hard-coded listing data. usp_Property_Search only ever
 * returns Approved rows here, so this section can never surface a Pending/
 * Rejected/Withdrawn listing to an anonymous visitor.
 *
 * A failure here is contained to this section (Alert, not a thrown error)
 * so a slow/unavailable backend never blocks the rest of the homepage from
 * rendering.
 */
export function HomeFeaturedProperties() {
  const [response, setResponse] = useState<PropertySearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // No synchronous setIsLoading(true)/setLoadError(null) here - this effect
  // only ever runs once on mount (isLoading already starts true via
  // useState above), matching BrowsePropertiesPage's own
  // "no setState reachable from an effect" convention. There is no
  // subsequent retry trigger on this component that would need to reset
  // these before a second fetch.
  const loadFeatured = useCallback(() => {
    searchProperties({ sortBy: 'Newest', pageNumber: 1, pageSize: FEATURED_COUNT })
      .then((result) => {
        setResponse(result);
        setLoadError(null);
      })
      .catch((error: unknown) => {
        setLoadError(error instanceof ApiError ? error.message : 'Could not load featured properties right now.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  useEffect(() => {
    loadFeatured();
  }, [loadFeatured]);

  return (
    <Box sx={{ bgcolor: 'background.paper', py: { xs: 6, md: 9 } }}>
      <Container maxWidth="lg">
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={2}
          sx={{ mb: 4, justifyContent: 'space-between', alignItems: { xs: 'flex-start', sm: 'center' } }}
        >
          <Box>
            <Typography variant="h4" component="h2" sx={{ fontWeight: 400 }}>
              Recently Approved Properties
            </Typography>
            <Typography color="text.secondary">A sample of listings that have passed administrator review.</Typography>
          </Box>
          <Button component={RouterLink} to="/properties" endIcon={<ArrowForwardIcon />}>
            View All Properties
          </Button>
        </Stack>

        {isLoading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 5 }}>
            <CircularProgress />
          </Box>
        )}

        {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

        {!isLoading && !loadError && response && response.items.length === 0 && (
          <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
            <Typography color="text.secondary">No approved properties are available right now. Check back soon.</Typography>
          </Paper>
        )}

        {!isLoading && !loadError && response && response.items.length > 0 && (
          <Grid container spacing={3}>
            {response.items.map((listing) => (
              <Grid key={listing.propertyId} size={{ xs: 12, sm: 6, md: 4 }}>
                <PropertyCard listing={listing} to={`/properties/${listing.propertyId}`} showStatus={false} />
              </Grid>
            ))}
          </Grid>
        )}
      </Container>
    </Box>
  );
}
