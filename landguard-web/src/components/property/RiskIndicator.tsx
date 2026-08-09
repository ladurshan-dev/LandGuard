import { Box, Chip, Tooltip, Typography } from '@mui/material';
import type { ChipProps } from '@mui/material';
import type { FraudStatus, RiskLevel } from '../../types/property';

interface RiskIndicatorProps {
  riskLevel: RiskLevel;
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

const FRAUD_COLOR: Record<FraudStatus, ChipProps['color']> = {
  Clean: 'success',
  Suspicious: 'warning',
  Fraudulent: 'error',
};

/**
 * Shows a listing's fraud-engine output at a glance: risk band, fraud
 * status, and the raw score when available. Used on both the property
 * card (compact) and the details page (same component - the fields it
 * needs are identical, just rendered at a different size).
 */
export function RiskIndicator({ riskLevel, fraudStatus, riskScore, size = 'small' }: RiskIndicatorProps) {
  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
      <Tooltip title={`Risk level: ${riskLevel}`}>
        <Chip label={`Risk: ${riskLevel}`} color={RISK_COLOR[riskLevel]} size={size} variant="outlined" />
      </Tooltip>
      <Tooltip title={`Fraud status: ${fraudStatus}`}>
        <Chip label={fraudStatus} color={FRAUD_COLOR[fraudStatus]} size={size} />
      </Tooltip>
      {riskScore !== null && (
        <Typography variant="caption" color="text.secondary">
          Score {riskScore}/100
        </Typography>
      )}
    </Box>
  );
}
