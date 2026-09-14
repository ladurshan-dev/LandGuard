import { Box, Container, Grid, Paper, Typography } from '@mui/material';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import BadgeIcon from '@mui/icons-material/Badge';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import RateReviewIcon from '@mui/icons-material/RateReview';
import { landguardColors } from '../../theme/landguardTheme';

interface Feature {
  Icon: typeof FactCheckIcon;
  title: string;
  description: string;
}

/**
 * Four genuine, distinct mechanisms this system actually implements - kept
 * deliberately separate rather than blended into one vague "fraud
 * protection" claim, per the explicit instruction that Government Deed
 * Verification and Supporting Risk Indicators must never be described as
 * the same thing, and that Admin approval must read as a manual step, not
 * an automatic guarantee.
 */
const FEATURES: Feature[] = [
  {
    Icon: FactCheckIcon,
    title: 'Government Deed Comparison',
    description:
      "A seller's uploaded deed is compared against government registry records - owner details, property reference and deed number are checked for a match before a listing can proceed.",
  },
  {
    Icon: BadgeIcon,
    title: 'Seller Identity Verification',
    description:
      "A seller's national identity details are checked as part of account verification, so buyers can see a Verified Seller badge on a listing before requesting contact.",
  },
  {
    Icon: TrendingUpIcon,
    title: 'Supporting Risk Indicators',
    description:
      'Additional listing-level checks (such as unusual pricing) surface supporting risk indicators for internal review. These are a separate signal from deed verification, not a fraud verdict on their own.',
  },
  {
    Icon: RateReviewIcon,
    title: 'Manual Administrator Review',
    description:
      'No listing goes live automatically. An administrator reviews the deed comparison and supporting indicators and manually approves each property before buyers can see it.',
  },
];

export function HomeFeatures() {
  return (
    <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }}>
      <Typography variant="h4" component="h2" sx={{ textAlign: 'center', fontWeight: 400, mb: 1 }}>
        How LandGuard reviews every listing
      </Typography>
      <Typography sx={{ textAlign: 'center', color: 'text.secondary', maxWidth: 640, mx: 'auto', mb: 5 }}>
        Four separate checks, not one black-box score - so you know exactly what a listing has and hasn't been
        through.
      </Typography>

      <Grid container spacing={3}>
        {FEATURES.map(({ Icon, title, description }) => (
          <Grid key={title} size={{ xs: 12, sm: 6, md: 3 }}>
            <Paper variant="outlined" sx={{ p: 3, height: '100%' }}>
              <Box
                sx={{
                  width: 48,
                  height: 48,
                  borderRadius: '12px',
                  bgcolor: landguardColors.greenPale,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  mb: 2,
                }}
              >
                <Icon sx={{ color: landguardColors.green }} />
              </Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                {title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {description}
              </Typography>
            </Paper>
          </Grid>
        ))}
      </Grid>
    </Container>
  );
}
