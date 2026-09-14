import { Box, Button, Container, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import SearchIcon from '@mui/icons-material/Search';
import AddHomeWorkIcon from '@mui/icons-material/AddHomeWork';
import { landguardColors } from '../../theme/landguardTheme';
import { useListPropertyRoute } from './useListPropertyRoute';

/**
 * Closing call-to-action. Reuses useListPropertyRoute (the same
 * role-branching logic HomeHero uses) so the "List a Property" button
 * never appears for a signed-in Buyer/Admin here either.
 */
export function HomeCta() {
  const listPropertyRoute = useListPropertyRoute();

  return (
    <Box sx={{ bgcolor: landguardColors.charcoal, color: '#fff', py: { xs: 6, md: 8 } }}>
      <Container maxWidth="md" sx={{ textAlign: 'center' }}>
        <Typography variant="h4" component="h2" sx={{ fontWeight: 400, mb: 1.5 }}>
          Ready to explore land with more confidence?
        </Typography>
        <Typography sx={{ color: 'rgba(255,255,255,0.7)', mb: 4 }}>
          Browse deed-verified, administrator-approved listings, or list your own property today.
        </Typography>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'center' }}>
          <Button
            component={RouterLink}
            to="/properties"
            variant="contained"
            color="secondary"
            size="large"
            startIcon={<SearchIcon />}
          >
            Browse Properties
          </Button>

          {listPropertyRoute && (
            <Button
              component={RouterLink}
              to={listPropertyRoute}
              variant="outlined"
              size="large"
              startIcon={<AddHomeWorkIcon />}
              sx={{
                color: '#fff',
                borderColor: 'rgba(255,255,255,0.5)',
                '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.08)' },
              }}
            >
              List a Property
            </Button>
          )}
        </Stack>
      </Container>
    </Box>
  );
}
