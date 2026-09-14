import { Box, Button, Chip, Container, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import SearchIcon from '@mui/icons-material/Search';
import AddHomeWorkIcon from '@mui/icons-material/AddHomeWork';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import GppGoodIcon from '@mui/icons-material/GppGood';
import { landguardColors } from '../../theme/landguardTheme';
import { useListPropertyRoute } from './useListPropertyRoute';

/**
 * Homepage hero. Copy is deliberately limited to what is actually true of
 * this system: LandGuard compares a Seller's uploaded deed against
 * government registry records and requires manual Admin approval before a
 * listing is buyer-visible - it does not claim to "verify ownership",
 * "guarantee" a transaction, or process payments/legal transfer anywhere.
 * No fabricated platform statistics (listing counts, "fraud detection
 * rate" percentages, etc.) - none of that data exists to report honestly.
 *
 * The secondary "List Your Property" action is hidden entirely for a
 * signed-in Buyer/Admin (useListPropertyRoute returns null) rather than
 * sent somewhere that would 403 or mislead - see that hook's own doc
 * comment.
 */
export function HomeHero() {
  const listPropertyRoute = useListPropertyRoute();

  return (
    <Box
      sx={{
        position: 'relative',
        overflow: 'hidden',
        background: `linear-gradient(135deg, ${landguardColors.green} 0%, ${landguardColors.charcoal} 100%)`,
        color: '#fff',
      }}
    >
      <Box
        sx={{
          position: 'absolute',
          inset: 0,
          opacity: 0.6,
          background: `radial-gradient(circle at 15% 20%, ${landguardColors.greenLight}55, transparent 45%), radial-gradient(circle at 85% 85%, ${landguardColors.gold}33, transparent 40%)`,
          pointerEvents: 'none',
        }}
      />

      <Container maxWidth="lg" sx={{ position: 'relative', py: { xs: 7, md: 11 } }}>
        <Chip
          icon={<GppGoodIcon sx={{ color: `${landguardColors.goldLight} !important` }} />}
          label="Deed-verified property listings for Sri Lanka"
          sx={{
            bgcolor: 'rgba(200,146,42,0.20)',
            border: '1px solid rgba(200,146,42,0.4)',
            color: landguardColors.goldLight,
            fontWeight: 600,
            mb: 3,
          }}
        />

        <Typography
          variant="h2"
          component="h1"
          sx={{ fontWeight: 400, lineHeight: 1.15, mb: 2, fontSize: { xs: 32, sm: 42, md: 54 }, maxWidth: 780 }}
        >
          Land property listings, checked against the record before they reach you.
        </Typography>

        <Typography sx={{ color: 'rgba(255,255,255,0.78)', maxWidth: 620, fontSize: { xs: 16, md: 18 }, mb: 4 }}>
          Every LandGuard listing has its deed compared against government registry records and is reviewed
          by an administrator before it becomes visible to buyers. Browse with more confidence, and list
          your own property through a process built around that same check.
        </Typography>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <Button
            component={RouterLink}
            to="/properties"
            variant="contained"
            color="secondary"
            size="large"
            startIcon={<SearchIcon />}
            sx={{ px: 3.5, py: 1.3 }}
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
                px: 3.5,
                py: 1.3,
                color: '#fff',
                borderColor: 'rgba(255,255,255,0.5)',
                '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.08)' },
              }}
            >
              List Your Property
            </Button>
          )}
        </Stack>

        <Stack direction="row" spacing={0.75} sx={{ mt: 4, alignItems: 'center', color: 'rgba(255,255,255,0.65)' }}>
          <FactCheckIcon fontSize="small" />
          <Typography variant="body2">Government deed comparison &middot; Manual administrator approval</Typography>
        </Stack>
      </Container>
    </Box>
  );
}
