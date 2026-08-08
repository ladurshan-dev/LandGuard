import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Grid,
  MenuItem,
  Pagination,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyCard } from '../../../components/property/PropertyCard';
import { searchProperties } from '../../../services/propertyService';
import { ApiError } from '../../../utils/apiError';
import type { PropertySearchRequest, PropertySearchResponse, PropertySortOption, RiskLevel } from '../../../types/property';

const PAGE_SIZE = 12;

/** Empty-string sentinel for the "no filter" option of the district-free-text/riskLevel selects, since MUI Select can't bind directly to `undefined`. */
const ANY_RISK_LEVEL = '';

interface FilterFormState {
  keyword: string;
  district: string;
  minPrice: string;
  maxPrice: string;
  minSize: string;
  maxSize: string;
  riskLevel: RiskLevel | typeof ANY_RISK_LEVEL;
  sortBy: PropertySortOption;
}

const EMPTY_FILTERS: FilterFormState = {
  keyword: '',
  district: '',
  minPrice: '',
  maxPrice: '',
  minSize: '',
  maxSize: '',
  riskLevel: ANY_RISK_LEVEL,
  sortBy: 'Newest',
};

function toSearchRequest(filters: FilterFormState, pageNumber: number): PropertySearchRequest {
  return {
    keyword: filters.keyword.trim() === '' ? undefined : filters.keyword.trim(),
    district: filters.district.trim() === '' ? undefined : filters.district.trim(),
    minPrice: filters.minPrice.trim() === '' ? undefined : Number(filters.minPrice),
    maxPrice: filters.maxPrice.trim() === '' ? undefined : Number(filters.maxPrice),
    minSize: filters.minSize.trim() === '' ? undefined : Number(filters.minSize),
    maxSize: filters.maxSize.trim() === '' ? undefined : Number(filters.maxSize),
    riskLevel: filters.riskLevel === ANY_RISK_LEVEL ? undefined : filters.riskLevel,
    sortBy: filters.sortBy,
    pageNumber,
    pageSize: PAGE_SIZE,
  };
}

/**
 * Buyer property search - GET /api/properties (AllowAnonymous, but only
 * reachable from an authenticated Buyer route here). The backend's own
 * usp_Property_Search only ever returns Approved listings to this
 * endpoint, so this page never needs to filter by status itself - doing
 * so would just be duplicating (and risking disagreeing with) a decision
 * the backend has already made.
 */
export default function BrowsePropertiesPage() {
  const { user } = useAuth();

  const [formValues, setFormValues] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);

  const [response, setResponse] = useState<PropertySearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Promise-chained, not async/await - see SellerPropertiesPage.loadListings
  // for why (the compiler's "no setState reachable from an effect" lint
  // rule doesn't see past the .then/.catch/.finally closures the same way
  // it sees through an async function's own await continuations).
  const runSearch = useCallback((request: PropertySearchRequest) => {
    searchProperties(request)
      .then((result) => {
        setResponse(result);
        setLoadError(null);
      })
      .catch((error: unknown) => {
        setLoadError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  // No synchronous setIsLoading(true) here - the effect itself only ever
  // calls runSearch (whose own state updates are deferred inside
  // .then/.catch/.finally, so they're not "reachable" from the effect for
  // the compiler's set-state-in-effect check the same way a direct call
  // would be). The "show a spinner for this request" transition instead
  // happens in the event handlers below, which set isLoading(true) before
  // changing the filters/page state that this effect depends on - a plain
  // user-interaction state update, not an effect one, so it isn't subject
  // to the same rule.
  useEffect(() => {
    runSearch(toSearchRequest(appliedFilters, pageNumber));
  }, [appliedFilters, pageNumber, runSearch]);

  if (!user) {
    return null;
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setIsLoading(true);
    setPageNumber(1);
    // A fresh object (not the formValues reference itself) so re-submitting
    // identical filter values still changes the effect's dependency and
    // reliably fires a reload rather than leaving isLoading stuck true.
    setAppliedFilters({ ...formValues });
  };

  const handleClear = () => {
    setFormValues(EMPTY_FILTERS);
    setIsLoading(true);
    setPageNumber(1);
    setAppliedFilters({ ...EMPTY_FILTERS });
  };

  const handlePageChange = (value: number) => {
    setIsLoading(true);
    setPageNumber(value);
  };

  const totalPages = response ? Math.max(1, Math.ceil(response.totalRecords / response.pageSize)) : 1;

  return (
    <DashboardLayout title="Browse Properties" user={user} maxWidth="lg">
      <Typography variant="h5" component="h1" sx={{ mb: 3 }}>
        Browse Properties
      </Typography>

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Box component="form" onSubmit={handleSubmit}>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                label="Keyword"
                fullWidth
                size="small"
                value={formValues.keyword}
                onChange={(event) => setFormValues((prev) => ({ ...prev, keyword: event.target.value }))}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                label="District"
                fullWidth
                size="small"
                value={formValues.district}
                onChange={(event) => setFormValues((prev) => ({ ...prev, district: event.target.value }))}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 1.5 }}>
              <TextField
                label="Min Price"
                type="number"
                fullWidth
                size="small"
                value={formValues.minPrice}
                onChange={(event) => setFormValues((prev) => ({ ...prev, minPrice: event.target.value }))}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 1.5 }}>
              <TextField
                label="Max Price"
                type="number"
                fullWidth
                size="small"
                value={formValues.maxPrice}
                onChange={(event) => setFormValues((prev) => ({ ...prev, maxPrice: event.target.value }))}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 1.5 }}>
              <TextField
                label="Min Size"
                type="number"
                fullWidth
                size="small"
                value={formValues.minSize}
                onChange={(event) => setFormValues((prev) => ({ ...prev, minSize: event.target.value }))}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 1.5 }}>
              <TextField
                label="Max Size"
                type="number"
                fullWidth
                size="small"
                value={formValues.maxSize}
                onChange={(event) => setFormValues((prev) => ({ ...prev, maxSize: event.target.value }))}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <TextField
                select
                label="Risk Level"
                fullWidth
                size="small"
                value={formValues.riskLevel}
                onChange={(event) =>
                  setFormValues((prev) => ({ ...prev, riskLevel: event.target.value as FilterFormState['riskLevel'] }))
                }
              >
                <MenuItem value={ANY_RISK_LEVEL}>Any</MenuItem>
                <MenuItem value="Low">Low</MenuItem>
                <MenuItem value="Medium">Medium</MenuItem>
                <MenuItem value="High">High</MenuItem>
              </TextField>
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2 }}>
              <TextField
                select
                label="Sort By"
                fullWidth
                size="small"
                value={formValues.sortBy}
                onChange={(event) =>
                  setFormValues((prev) => ({ ...prev, sortBy: event.target.value as PropertySortOption }))
                }
              >
                <MenuItem value="Newest">Newest</MenuItem>
                <MenuItem value="PriceAsc">Price: Low to High</MenuItem>
                <MenuItem value="PriceDesc">Price: High to Low</MenuItem>
                <MenuItem value="RiskAsc">Risk: Low to High</MenuItem>
              </TextField>
            </Grid>
            <Grid size={{ xs: 12, sm: 4, md: 2 }} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Button type="submit" variant="contained" startIcon={<SearchIcon />} fullWidth>
                Search
              </Button>
              <Button type="button" variant="text" onClick={handleClear}>
                Clear
              </Button>
            </Grid>
          </Grid>
        </Box>
      </Paper>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && response && response.items.length === 0 && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No properties match your filters.</Typography>
        </Paper>
      )}

      {!isLoading && !loadError && response && response.items.length > 0 && (
        <>
          <Grid container spacing={2}>
            {response.items.map((listing) => (
              <Grid key={listing.propertyId} size={{ xs: 12, sm: 6, md: 4 }}>
                <PropertyCard listing={listing} to={`/buyer/properties/${listing.propertyId}`} showStatus={false} />
              </Grid>
            ))}
          </Grid>

          {totalPages > 1 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
              <Pagination
                count={totalPages}
                page={pageNumber}
                onChange={(_event, value) => handlePageChange(value)}
                color="primary"
              />
            </Box>
          )}
        </>
      )}
    </DashboardLayout>
  );
}
