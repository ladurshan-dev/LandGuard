import { useState } from 'react';
import { Box, Card, CardActionArea, CardContent, CardMedia, Typography } from '@mui/material';
import PlaceIcon from '@mui/icons-material/Place';
import StraightenIcon from '@mui/icons-material/Straighten';
import { Link as RouterLink } from 'react-router-dom';
import { resolveAssetUrl } from '../../api/axios';
import { PropertyStatusChip } from './PropertyStatusChip';
import { RiskIndicator } from './RiskIndicator';
import { PropertyImagePlaceholder } from './PropertyImagePlaceholder';
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
 *
 * Buyer privacy requirement: riskLevel/fraudStatus/riskScore are null on
 * the listing whenever the backend redacted them (any caller who isn't
 * this listing's owner or an Admin - see PropertyListingResult.riskScore's
 * doc comment) - the risk indicator below is simply not rendered in that
 * case. This component stays role-agnostic on purpose: it never checks who
 * the current user is, it only ever reacts to whether the data is present,
 * so a Buyer genuinely never receives the fields to render, rather than
 * this component being trusted to hide something it was given.
 */
export function PropertyCard({ listing, to, showStatus = true }: PropertyCardProps) {
  // Bug fix (manual Buyer testing - "Galle Price Anomaly Test" rendered a
  // broken <img> icon with no uploaded images): coverImageUrl is nullable,
  // and the previous code always rendered a CardMedia<img> regardless -
  // when coverImageUrl was null/empty, `image` was passed as `undefined`,
  // which MUI's CardMedia still renders as a real <img> with no usable
  // src, and every browser shows that as a broken-image icon (with `alt`
  // as the visible fallback text - exactly the "title as alt text" the
  // report described). `.trim()` also treats a whitespace-only value the
  // same as missing, per the report's explicit requirement.
  //
  // `failedImageUrl` tracks the resolved URL of the last onError (a real
  // URL that failed to *load*, e.g. a deleted file - a different failure
  // mode than "no URL at all", but both must fall back to the same
  // placeholder). Comparing it against the *current* resolved URL (rather
  // than a plain boolean) means a fresh/changed coverImageUrl on a later
  // re-render of the same card instance is always retried instead of
  // staying stuck on a stale failure.
  const [failedImageUrl, setFailedImageUrl] = useState<string | null>(null);

  const trimmedCoverImageUrl = listing.coverImageUrl?.trim();
  const resolvedImageUrl = trimmedCoverImageUrl ? resolveAssetUrl(trimmedCoverImageUrl) : null;
  const showPlaceholder = !resolvedImageUrl || failedImageUrl === resolvedImageUrl;

  return (
    <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardActionArea component={RouterLink} to={to} sx={{ flexGrow: 1, alignItems: 'stretch' }}>
        {showPlaceholder ? (
          <PropertyImagePlaceholder height={160} label={`No image available for ${listing.title}`} />
        ) : (
          <CardMedia
            component="img"
            height="160"
            image={resolvedImageUrl}
            alt={listing.title}
            sx={{ bgcolor: 'grey.200', objectFit: 'cover' }}
            onError={() => setFailedImageUrl(resolvedImageUrl)}
          />
        )}
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

          {listing.riskLevel !== null && listing.fraudStatus !== null && (
            <RiskIndicator riskLevel={listing.riskLevel} fraudStatus={listing.fraudStatus} riskScore={listing.riskScore} />
          )}
        </CardContent>
      </CardActionArea>
    </Card>
  );
}
