import { Box, Typography } from '@mui/material';
import ImageNotSupportedIcon from '@mui/icons-material/ImageNotSupported';

interface PropertyImagePlaceholderProps {
  /** Matches the real image slot it stands in for, so swapping between the two never shifts layout - a pixel number (PropertyCard's CardMedia height="160") or a CSS size like '100%' (PropertyImageGallery's per-item tiles, which size themselves via an aspect-ratio wrapper rather than a fixed height). */
  height?: number | string;
  /** Both the visible caption and the accessible name (via aria-label) - callers pass context-appropriate copy ("No image available" for a thumbnail, "No images have been uploaded for this property yet." for a full gallery), rather than this component guessing which situation it's in. */
  label: string;
}

/**
 * The one shared "no image" visual across the app - used by PropertyCard
 * (bug fix: a listing with no cover image, or whose cover image URL fails
 * to load, previously rendered a bare broken-image `<img>` icon instead of
 * this) and PropertyImageGallery's own zero-images state, so a Buyer/
 * Seller/Admin sees the same fallback tile everywhere a property photo
 * would otherwise go. Deliberately a plain `<Box>`, never an `<img>` -
 * there is no URL to fail to load here, so there is nothing for a browser
 * to ever render as broken.
 */
export function PropertyImagePlaceholder({ height = 160, label }: PropertyImagePlaceholderProps) {
  return (
    <Box
      role="img"
      aria-label={label}
      sx={{
        height,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 0.5,
        bgcolor: 'grey.200',
        color: 'text.disabled',
        borderRadius: 1,
      }}
    >
      <ImageNotSupportedIcon fontSize="large" aria-hidden="true" />
      <Typography variant="caption" color="text.disabled" sx={{ px: 2, textAlign: 'center' }}>
        {label}
      </Typography>
    </Box>
  );
}
