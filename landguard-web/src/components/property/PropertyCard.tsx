import type { SyntheticEvent } from 'react';
import { Box, Card, CardActionArea, CardContent, CardMedia, Typography } from '@mui/material';
import PlaceIcon from '@mui/icons-material/Place';
import StraightenIcon from '@mui/icons-material/Straighten';
import { Link as RouterLink } from 'react-router-dom';
import { resolveAssetUrl } from '../../api/axios';
import { PropertyStatusChip } from './PropertyStatusChip';
import { RiskIndicator } from './RiskIndicator';
import { formatCurrency, formatSize } from '../../utils/format';
import type { PropertyListingResult } from '../../types/property';

interface PropertyCardProps {
  listing: PropertyListingResult;
  /** Route to navigate to on click - the details page appropriate for the caller's role (seller/buyer/admin each have their own). */
  to: string;
  /** Whether to show the status chip - buyers never need this since search only ever returns Approved listings, but showing it does no harm and keeps this component role-agnostic. */
  showStatus?: boolean;
}

/**
 * Compact summary card for a listing - the one presentational unit reused
 * by the seller's own-listings grid, the buyer's search results grid, and
 * the admin's listings grid. Everything it reads (title, location, price,
 * size, status, risk/fraud) comes straight from PropertyListingResult /
 * PropertySearchResult (a superset), never from data invented for display.
 */
export function PropertyCard({ listing, to, showStatus = true }: PropertyCardProps) {
  return (
    <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardActionArea component={RouterLink} to={to} sx={{ flexGrow: 1, alignItems: 'stretch' }}>
        <CardMedia
          component="img"
          height="160"
          image={listing.coverImageUrl ? resolveAssetUrl(listing.coverImageUrl) : undefined}
          alt={listing.title}
          sx={{ bgcolor: 'grey.200', objectFit: 'cover' }}
          onError={(event: SyntheticEvent<HTMLImageElement>) => {
            event.currentTarget.style.display = 'none';
          }}
        />
        <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 1 }}>
            <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
              {listing.title}
            </Typography>
            {showStatus && <PropertyStatusChip status={listing.status} />}
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: 'text.secondary' }}>
            <PlaceIcon fontSize="small" />
            <Typography variant="body2">
              {listing.location}
              {listing.district ? `, ${listing.district}` : ''}
            </Typography>
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: 'text.secondary' }}>
            <StraightenIcon fontSize="small" />
            <Typography variant="body2">{formatSize(listing.size)}</Typography>
          </Box>

          <Typography variant="h6" color="primary" sx={{ fontWeight: 700 }}>
            {formatCurrency(listing.price)}
          </Typography>

          <RiskIndicator riskLevel={listing.riskLevel} fraudStatus={listing.fraudStatus} riskScore={listing.riskScore} />
        </CardContent>
      </CardActionArea>
    </Card>
  );
}
