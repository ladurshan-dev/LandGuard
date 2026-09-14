import { useState } from 'react';
import type { FormEvent } from 'react';
import { Box, Button, Container, Grid, Paper, TextField } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import SearchIcon from '@mui/icons-material/Search';

/**
 * Homepage quick-search - deliberately only offers the fields
 * PropertySearchRequest actually supports (keyword, district, an optional
 * price range). No "property type" field: the backend has no such filter,
 * so one is not invented here just because the design reference had it.
 * Submitting navigates to /properties?<query>, which
 * PublicBrowsePropertiesPage reads back out and feeds straight into the
 * existing searchProperties() service - this component never calls the
 * API itself.
 */
export function HomeSearch() {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [district, setDistrict] = useState('');
  const [minPrice, setMinPrice] = useState('');
  const [maxPrice, setMaxPrice] = useState('');

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();

    const params = new URLSearchParams();
    if (keyword.trim()) params.set('keyword', keyword.trim());
    if (district.trim()) params.set('district', district.trim());
    if (minPrice.trim()) params.set('minPrice', minPrice.trim());
    if (maxPrice.trim()) params.set('maxPrice', maxPrice.trim());

    navigate(params.toString() ? `/properties?${params.toString()}` : '/properties');
  };

  return (
    <Container maxWidth="lg" sx={{ mt: { xs: -4, md: -5 }, position: 'relative', zIndex: 1 }}>
      <Paper elevation={0} sx={{ p: { xs: 2, sm: 3 }, border: '1px solid', borderColor: 'divider' }}>
        <Box component="form" onSubmit={handleSubmit}>
          <Grid container spacing={2} sx={{ alignItems: 'center' }}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                label="Keyword"
                placeholder="e.g. land, house, farm"
                fullWidth
                size="small"
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                label="District"
                placeholder="e.g. Kandy"
                fullWidth
                size="small"
                value={district}
                onChange={(event) => setDistrict(event.target.value)}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 2 }}>
              <TextField
                label="Min Price"
                type="number"
                fullWidth
                size="small"
                value={minPrice}
                onChange={(event) => setMinPrice(event.target.value)}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3, md: 2 }}>
              <TextField
                label="Max Price"
                type="number"
                fullWidth
                size="small"
                value={maxPrice}
                onChange={(event) => setMaxPrice(event.target.value)}
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 2 }}>
              <Button type="submit" variant="contained" fullWidth size="large" startIcon={<SearchIcon />}>
                Search
              </Button>
            </Grid>
          </Grid>
        </Box>
      </Paper>
    </Container>
  );
}
