import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  Grid,
  MenuItem,
  Pagination,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { PublicLayout } from '../../layouts/PublicLayout';
import { PropertyCard } from '../../components/property/PropertyCard';
import { searchProperties } from '../../services/propertyService';
import { ApiError } from '../../utils/apiError';
import type { PropertySearchRequest, PropertySearchResponse, PropertySortOption } from '../../types/property';

const PAGE_SIZE = 12;

interface FilterFormState {
  keyword: string;
  district: string;
  minPrice: string;
  maxPrice: string;
  minSize: string;
  maxSize: string;
  sortBy: PropertySortOption;
}

const EMPTY_FILTERS: FilterFormState = {
  keyword: '',
  district: '',
  minPrice: '',
  maxPrice: '',
  minSize: '',
  maxSize: '',
  sortBy: 'Newest',
};

const SORT_OPTIONS: PropertySortOption[] = ['Newest', 'Oldest', 'PriceAsc', 'PriceDesc'];

function isSortOption(value: string | null): value is PropertySortOption {
  return value !== null && (SORT_OPTIONS as string[]).includes(value);
}

/** Reads the same filter shape back out of a URLSearchParams (from HomeSearch's navigate, or a shared/bookmarked link). */
function filtersFromSearchParams(params: URLSearchParams): FilterFormState {
  return {
    keyword: params.get('keyword') ?? '',
    district: params.get('district') ?? '',
    minPrice: params.get('minPrice') ?? '',
    maxPrice: params.get('maxPrice') ?? '',
    minSize: params.get('minSize') ?? '',
    maxSize: params.get('maxSize') ?? '',
    sortBy: isSortOption(params.get('sortBy')) ? (params.get('sortBy') as PropertySortOption) : 'Newest',
  };
}

function filtersToSearchParams(filters: FilterFormState): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.keyword.trim()) params.set('keyword', filters.keyword.trim());
  if (filters.district.trim()) params.set('district', filters.district.trim());
  if (filters.minPrice.trim()) params.set('minPrice', filters.minPrice.trim());
  if (filters.maxPrice.trim()) params.set('maxPrice', filters.maxPrice.trim());
  if (filters.minSize.trim()) params.set('minSize', filters.minSize.trim());
  if (filters.maxSize.trim()) params.set('maxSize', filters.maxSize.trim());
  if (filters.sortBy !== 'Newest') params.set('sortBy', filters.sortBy);
  return params;
}

function toSearchRequest(filters: FilterFormState, pageNumber: number): PropertySearchRequest {
  return {
    keyword: filters.keyword.trim() === '' ? undefined : filters.keyword.trim(),
    district: filters.district.trim() === '' ? undefined : filters.district.trim(),
    minPrice: filters.minPrice.trim() === '' ? undefined : Number(filters.minPrice),
    maxPrice: filters.maxPrice.trim() === '' ? undefined : Number(filters.maxPrice),
    minSize: filters.minSize.trim() === '' ? undefined : Number(filters.minSize),
    maxSize: filters.maxSize.trim() === '' ? undefined : Number(filters.maxSize),
    // No riskLevel filter/sort here, same Buyer-privacy requirement
    // BrowsePropertiesPage already follows - see that file's own doc
    // comment. This page is even more exposed (fully public, no auth at
    // all), so the same restriction applies at least as strongly.
    sortBy: filters.sortBy,
    pageNumber,
    pageSize: PAGE_SIZE,
  };
}

/**
 * Public, unauthenticated property search - additive to (not a replacement
 * for) the protected "/buyer/properties" page. Calls the exact same
 * AllowAnonymous GET /api/properties (searchProperties) BrowsePropertiesPage
 * already uses; usp_Property_Search only ever returns Approved listings to
 * this endpoint regardless of caller, so this page needs no status filter
 * of its own. Initial filters are read from the URL's query string (set by
 * HomeSearch's navigate, or a bookmarked/shared link) so a visitor arriving
 * from the homepage search sees the same results reflected here.
 */
export default function PublicBrowsePropertiesPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const [formValues, setFormValues] = useState<FilterFormState>(() => filtersFromSearchParams(searchParams));
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(() => filtersFromSearchParams(searchParams));
  const [pageNumber, setPageNumber] = useState(1);

  const [response, setResponse] = useState<PropertySearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Promise-chained - matches BrowsePropertiesPage.runSearch/SellerPropertiesPage.loadListings.
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

  useEffect(() => {
    runSearch(toSearchRequest(appliedFilters, pageNumber));
  }, [appliedFilters, pageNumber, runSearch]);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setIsLoading(true);
    setPageNumber(1);
    setAppliedFilters({ ...formValues });
    setSearchParams(filtersToSearchParams(formValues));
  };

  const handleClear = () => {
    setFormValues(EMPTY_FILTERS);
    setIsLoading(true);
    setPageNumber(1);
    setAppliedFilters({ ...EMPTY_FILTERS });
    setSearchParams(new URLSearchParams());
  };

  const handlePageChange = (value: number) => {
    setIsLoading(true);
    setPageNumber(value);
  };

  const totalPages = response ? Math.max(1, Math.ceil(response.totalRecords / response.pageSize)) : 1;

  return (
    <PublicLayout>
      <Container maxWidth="lg" sx={{ py: { xs: 3, md: 5 } }}>
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
                  label="Sort By"
                  fullWidth
                  size="small"
                  value={formValues.sortBy}
                  onChange={(event) =>
                    setFormValues((prev) => ({ ...prev, sortBy: event.target.value as PropertySortOption }))
                  }
                >
                  <MenuItem value="Newest">Newest</MenuItem>
                  <MenuItem value="Oldest">Oldest</MenuItem>
                  <MenuItem value="PriceAsc">Price: Low to High</MenuItem>
                  <MenuItem value="PriceDesc">Price: High to Low</MenuItem>
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
                  <PropertyCard listing={listing} to={`/properties/${listing.propertyId}`} showStatus={false} />
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
      </Container>
    </PublicLayout>
  );
}
