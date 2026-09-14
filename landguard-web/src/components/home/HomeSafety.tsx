import { Box, Container, Grid, Stack, Typography } from '@mui/material';
import GppGoodIcon from '@mui/icons-material/GppGood';
import VerifiedIcon from '@mui/icons-material/Verified';
import ReportProblemIcon from '@mui/icons-material/ReportProblem';
import { landguardColors } from '../../theme/landguardTheme';

/**
 * A short, on-page trust/safety section rather than a dedicated "/safety"
 * route - no such page exists in this app, and the brief was explicit that
 * a small homepage section is sufficient rather than building a new module
 * just for this. No call-to-action button here on purpose: there is
 * nowhere truthful to send it (no Safety/About page to link to).
 */
export function HomeSafety() {
  return (
    <Box sx={{ bgcolor: landguardColors.greenPale, py: { xs: 6, md: 8 } }}>
      <Container maxWidth="lg">
        <Typography variant="h4" component="h2" sx={{ fontWeight: 400, textAlign: 'center', mb: 1 }}>
          Buyer due diligence still matters
        </Typography>
        <Typography sx={{ textAlign: 'center', color: 'text.secondary', maxWidth: 680, mx: 'auto', mb: 4 }}>
          LandGuard's checks reduce risk, but Admin approval is not a legal guarantee of ownership or title. We
          encourage every buyer to do their own research before proceeding with a transaction.
        </Typography>

        <Grid container spacing={3} sx={{ justifyContent: 'center' }}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
              <GppGoodIcon sx={{ color: landguardColors.green, fontSize: 32 }} />
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Deed-checked listings
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Every approved listing has had its deed compared against government registry records.
              </Typography>
            </Stack>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
              <VerifiedIcon sx={{ color: landguardColors.green, fontSize: 32 }} />
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Identity-checked sellers
              </Typography>
              <Typography variant="body2" color="text.secondary">
                A Verified Seller badge means that seller's identity details have been checked.
              </Typography>
            </Stack>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
              <ReportProblemIcon sx={{ color: landguardColors.gold, fontSize: 32 }} />
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Always do your own checks
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Verify details independently before making any payment or legal commitment.
              </Typography>
            </Stack>
          </Grid>
        </Grid>
      </Container>
    </Box>
  );
}
