import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
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
  Divider,
  Grid,
  IconButton,
  MenuItem,
  Pagination,
  Paper,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import SearchIcon from '@mui/icons-material/Search';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { DashboardLayout } from '../../../layouts/DashboardLayout';
import { useAuth } from '../../../hooks/useAuth';
import { PropertyStatusChip } from '../../../components/property/PropertyStatusChip';
import { RiskIndicator } from '../../../components/property/RiskIndicator';
import { deleteProperty, searchProperties } from '../../../services/propertyService';
import { formatCurrency, formatSize } from '../../../utils/format';
import { ApiError } from '../../../utils/apiError';
import type { PropertySearchRequest, PropertySearchResponse, PropertySortOption, RiskLevel } from '../../../types/property';

const PAGE_SIZE = 12;
const ANY_RISK_LEVEL = '';

interface FilterFormState {
  keyword: string;
  district: string;
  riskLevel: RiskLevel | typeof ANY_RISK_LEVEL;
  sortBy: PropertySortOption;
}

const EMPTY_FILTERS: FilterFormState = {
  keyword: '',
  district: '',
  riskLevel: ANY_RISK_LEVEL,
  sortBy: 'Newest',
};

function toSearchRequest(filters: FilterFormState, pageNumber: number): PropertySearchRequest {
  return {
    keyword: filters.keyword.trim() === '' ? undefined : filters.keyword.trim(),
    district: filters.district.trim() === '' ? undefined : filters.district.trim(),
    riskLevel: filters.riskLevel === ANY_RISK_LEVEL ? undefined : filters.riskLevel,
    sortBy: filters.sortBy,
    pageNumber,
    pageSize: PAGE_SIZE,
  };
}

/**
 * Admin property oversight. Built on the same GET /api/properties search
 * PropertyController exposes to everyone - which is important to be
 * upfront about: usp_Property_Search returns Approved listings only,
 * regardless of caller role (PropertyService.SearchAsync applies no
 * role-based filtering of its own), so this grid shows the same
 * Approved-only result set a buyer would see, not a full moderation queue
 * of Pending/Flagged submissions. There is currently no backend endpoint
 * that lists non-Approved properties in bulk - inventing a client-side
 * substitute (e.g. guessing ids, or scanning every seller) would just be
 * unreliable and is explicitly out of scope. What GetByIdAsync *does*
 * already support for an Admin caller is fetching any single property by
 * id regardless of status - so the lookup box below, backed by the exact
 * same getPropertyById already used elsewhere, is the one genuine way
 * this page can reach a Pending/Flagged/Rejected listing today.
 */
export default function AdminPropertiesPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [formValues, setFormValues] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);

  const [response, setResponse] = useState<PropertySearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [lookupId, setLookupId] = useState('');

  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  // Promise-chained - see SellerPropertiesPage.loadListings for why.
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

  if (!user) {
    return null;
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setIsLoading(true);
    setPageNumber(1);
    setAppliedFilters({ ...formValues });
  };

  const handlePageChange = (value: number) => {
    setIsLoading(true);
    setPageNumber(value);
  };

  const handleLookup = (event: FormEvent) => {
    event.preventDefault();
    const trimmed = lookupId.trim();
    if (trimmed !== '' && Number.isInteger(Number(trimmed))) {
      navigate(`/admin/properties/${trimmed}`);
    }
  };

  const handleConfirmDelete = async () => {
    if (pendingDeleteId === null) {
      return;
    }

    setIsDeleting(true);
    setDeleteError(null);

    try {
      await deleteProperty(pendingDeleteId);
      setResponse((current) =>
        current ? { ...current, items: current.items.filter((item) => item.propertyId !== pendingDeleteId) } : current,
      );
      setPendingDeleteId(null);
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  };

  const totalPages = response ? Math.max(1, Math.ceil(response.totalRecords / response.pageSize)) : 1;

  return (
    <DashboardLayout title="Property Oversight" user={user} maxWidth="lg">
      <Typography variant="h5" component="h1" sx={{ mb: 3 }}>
        Property Oversight
      </Typography>

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          Look up a property by ID
        </Typography>
        <Box component="form" onSubmit={handleLookup} sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <TextField
            size="small"
            label="Property ID"
            type="number"
            value={lookupId}
            onChange={(event) => setLookupId(event.target.value)}
            slotProps={{ htmlInput: { min: 1 } }}
          />
          <Button type="submit" variant="outlined">
            View
          </Button>
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          Use this to inspect a Pending, Flagged or Rejected listing directly - the search below only ever returns
          Approved listings, the same as a buyer would see.
        </Typography>
      </Paper>

      <Divider sx={{ mb: 3 }} />

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Box component="form" onSubmit={handleSubmit}>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                label="Keyword"
                fullWidth
                size="small"
                value={formValues.keyword}
                onChange={(event) => setFormValues((prev) => ({ ...prev, keyword: event.target.value }))}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                label="District"
                fullWidth
                size="small"
                value={formValues.district}
                onChange={(event) => setFormValues((prev) => ({ ...prev, district: event.target.value }))}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 2 }}>
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
            <Grid size={{ xs: 6, sm: 2 }}>
              <Button type="submit" variant="contained" startIcon={<SearchIcon />} fullWidth sx={{ height: '100%' }}>
                Search
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

                    <Typography variant="body2" color="text.secondary">
                      Seller: {listing.sellerName}
                    </Typography>

                    <Typography variant="h6" color="primary" sx={{ fontWeight: 700, mt: 1 }}>
                      {formatCurrency(listing.price)}
                    </Typography>

                    <Box sx={{ mt: 1 }}>
                      {/*
                        Non-null assertions: riskLevel/fraudStatus are only
                        ever null when the backend redacted them for a
                        non-Admin caller - see PropertyListingResult.
                        riskScore's doc comment. This page is Admin-only, so
                        this caller is always an Admin - never redacted.
                      */}
                      <RiskIndicator riskLevel={listing.riskLevel!} fraudStatus={listing.fraudStatus!} riskScore={listing.riskScore} />
                    </Box>

                    <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 0.5, mt: 1.5 }}>
                      <Tooltip title="View details">
                        <IconButton size="small" onClick={() => navigate(`/admin/properties/${listing.propertyId}`)}>
                          <VisibilityIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Delete">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => {
                            setDeleteError(null);
                            setPendingDeleteId(listing.propertyId);
                          }}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </Box>
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>

          {totalPages > 1 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
              <Pagination count={totalPages} page={pageNumber} onChange={(_event, value) => handlePageChange(value)} color="primary" />
            </Box>
          )}
        </>
      )}

      <Dialog open={pendingDeleteId !== null} onClose={() => setPendingDeleteId(null)}>
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
          <Button onClick={() => setPendingDeleteId(null)} disabled={isDeleting}>
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
