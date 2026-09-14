import { Box, Container, Grid, Typography } from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import RateReviewIcon from '@mui/icons-material/RateReview';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { landguardColors } from '../../theme/landguardTheme';

interface Step {
  Icon: typeof UploadFileIcon;
  title: string;
  description: string;
}

/**
 * The real, current listing lifecycle (PropertyStatus: Pending -> Approved,
 * with deed verification/fraud-engine checks running in between) - not a
 * generic marketing funnel. Matches what SellerPropertiesPage/
 * AdminPropertyReviewPage actually implement, described at a level a
 * public visitor can understand without exposing internal rule details.
 */
const STEPS: Step[] = [
  {
    Icon: UploadFileIcon,
    title: 'Seller submits a listing',
    description: 'A seller enters the property details and uploads their deed document.',
  },
  {
    Icon: FactCheckIcon,
    title: 'Deed comparison runs',
    description: "The uploaded deed is compared against government registry records, and supporting risk indicators are checked.",
  },
  {
    Icon: RateReviewIcon,
    title: 'An administrator reviews it',
    description: 'A LandGuard administrator manually reviews the results before making a decision - nothing is approved automatically.',
  },
  {
    Icon: VisibilityIcon,
    title: 'Approved listings go live',
    description: 'Once approved, the property becomes visible to buyers here and in Browse Properties.',
  },
];

export function HomeHowItWorks() {
  return (
    <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }}>
      <Typography variant="h4" component="h2" sx={{ textAlign: 'center', fontWeight: 400, mb: 5 }}>
        How a listing gets approved
      </Typography>

      <Grid container spacing={3}>
        {STEPS.map(({ Icon, title, description }, index) => (
          <Grid key={title} size={{ xs: 12, sm: 6, md: 3 }}>
            <Box sx={{ textAlign: 'center', px: 1 }}>
              <Box
                sx={{
                  width: 56,
                  height: 56,
                  borderRadius: '50%',
                  bgcolor: landguardColors.greenPale,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  mx: 'auto',
                  mb: 1.5,
                  position: 'relative',
                }}
              >
                <Icon sx={{ color: landguardColors.green }} />
                <Box
                  sx={{
                    position: 'absolute',
                    top: -6,
                    right: -6,
                    width: 22,
                    height: 22,
                    borderRadius: '50%',
                    bgcolor: 'secondary.main',
                    color: 'secondary.contrastText',
                    fontSize: 12,
                    fontWeight: 700,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  {index + 1}
                </Box>
              </Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 0.5 }}>
                {title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {description}
              </Typography>
            </Box>
          </Grid>
        ))}
      </Grid>
    </Container>
  );
}
