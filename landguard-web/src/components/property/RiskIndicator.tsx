import { Box, Chip, Tooltip, Typography } from '@mui/material';
import type { ChipProps } from '@mui/material';
import type { FraudStatus, RiskLevel } from '../../types/property';

interface RiskIndicatorProps {
  riskLevel: RiskLevel;
  /**
   * Accepted for backward compatibility with every existing call site
   * (PropertyListingResult/PropertySearchResult always carry it) but
   * deliberately not rendered here - see this component's own doc
   * comment for why "Clean"/"Suspicious"/"Fraudulent" no longer appears
   * as a compact, unexplained chip next to every listing.
   */
  fraudStatus: FraudStatus;
  /** 0-100 composite score from usp_Fraud_AnalyseProperty, null until the engine has run at least once (e.g. immediately after usp_Property_Create with no images yet). */
  riskScore: number | null;
  size?: ChipProps['size'];
}

const RISK_COLOR: Record<RiskLevel, ChipProps['color']> = {
  Low: 'success',
  Medium: 'warning',
  High: 'error',
};

/**
 * Compact, listing-card-sized rendering of the legacy supporting risk
 * engine's output - used on PropertyCard (browse/oversight/review grids)
 * and anywhere else a whole listing needs to show its risk band at a
 * glance, alongside the details-page PropertyFraudPanel (same underlying
 * data, full breakdown).
 *
 * Phase E (Supporting Risk Indicator Refactor): this used to also render
 * a second, prominently colored chip labelled "Clean"/"Suspicious"/
 * "Fraudulent" (dbo.FraudCheck.FraudStatus) right next to every property
 * card across every browse/oversight/review grid, with no explanation in
 * sight. That reads as an authoritative fraud verdict about the listing's
 * deed - it never was one; it is a banding of the same legacy numeric
 * score `riskLevel`/`riskScore` already show here. Government Deed
 * Verification (a separate section, a separate persisted record) is the
 * authoritative deed-authenticity evidence - see
 * DeedVerificationResultView/GovernmentDeedFraudDetectionResult. This
 * component no longer renders `fraudStatus` at all, anywhere, so a buyer
 * skimming a grid of cards never sees "Fraudulent" stamped on a listing
 * whose deed was never even government-verified yet. The prop stays on
 * the interface only so no call site needs to change.
 */
export function RiskIndicator({ riskLevel, riskScore, size = 'small' }: RiskIndicatorProps) {
  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
      <Tooltip title="Supporting listing-risk indicator - not a fraud verdict or a deed authenticity check.">
        <Chip label={`Risk: ${riskLevel}`} color={RISK_COLOR[riskLevel]} size={size} variant="outlined" />
      </Tooltip>
      {riskScore !== null && (
        <Typography variant="caption" color="text.secondary">
          Score {riskScore}/100
        </Typography>
      )}
    </Box>
  );
}
