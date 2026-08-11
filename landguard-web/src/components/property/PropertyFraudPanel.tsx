import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { formatDate } from '../../utils/format';
import type { FraudStatus, PropertyFraudRuleResult, RiskLevel } from '../../types/property';

interface PropertyFraudPanelProps {
  riskLevel: RiskLevel;
  /** Kept for API/type compatibility - deliberately not rendered here. See this file's own doc comment. */
  fraudStatus: FraudStatus;
  riskScore: number | null;
  riskSummary: string | null;
  riskGeneratedDate: string | null;
  fraudReport: PropertyFraudRuleResult[];
  /**
   * Shows each rule's raw point value/weight and its backend-authored
   * description as small secondary text, for Admin diagnostics only.
   * Seller/Buyer usage omits this (defaults to false) - the numeric
   * scoring stays available in the API/props either way (nothing is
   * deleted from PropertyFraudRuleResult), this only controls whether the
   * default UI surfaces it. See AdminPropertyDetailsPage's usage.
   */
  showRulePoints?: boolean;
}

/**
 * Renders the legacy numeric fraud/risk engine's output for one property -
 * shared by every role's details page, since PropertyDetail.fraudReport is
 * the exact same shape regardless of who's viewing it. Read-only reporting
 * of what usp_Fraud_AnalyseProperty/usp_Risk_GenerateReport already
 * decided; never lets the frontend approve/reject/flag anything.
 *
 * Phase E (Supporting Risk Indicator Refactor). This engine predates
 * Government Deed Verification and is now explicitly a SUPPORTING signal
 * only - see usp_Risk_GenerateReport's own Phase B/Phase E notes in
 * 04_StoredProcedures.sql. Previously this panel was titled "Fraud & Risk
 * Assessment" and led with a big colored "Clean"/"Suspicious"/"Fraudulent"
 * chip (dbo.FraudCheck.FraudStatus) - that reads as an authoritative fraud
 * verdict about the listing's deed. It never was one: it is a banding of
 * the same numeric RiskScore shown below it, computed from 7 listing-level
 * heuristics (price vs. district benchmark, image hash duplication,
 * seller account NIC format/verification, duplicate deed reference text,
 * seller history, geocoding validity, listing completeness) - none of
 * which compare the uploaded deed to the trusted government registry.
 * Government Deed Verification (a separate, persisted record - see
 * DeedVerificationResultView) is the authoritative deed-authenticity
 * evidence. This panel is renamed "Supporting Risk Indicators", no longer
 * shows FraudStatus as a chip anywhere, and leads with plain
 * Passed/Review-recommended language per rule instead of raw point values
 * (still available via `showRulePoints`, Admin-only).
 */
const RULE_DISPLAY: Record<string, { label: string; passedText: string; triggeredText: string }> = {
  PRICE_ANOMALY: {
    label: 'Price anomaly check',
    passedText: 'Listing price is in line with the district benchmark.',
    triggeredText: 'Listing price differs significantly from the district benchmark.',
  },
  IMAGE_DUPLICATE: {
    label: 'Duplicate image check',
    passedText: 'No listing images match another property.',
    triggeredText: 'An image on this listing matches another property.',
  },
  NIC_VERIFICATION: {
    label: 'Seller account NIC check',
    passedText: "Seller's account NIC is verified and unique.",
    triggeredText: "Seller's account NIC needs review (format, verification, or uniqueness). This is an account-level check - not a comparison of the deed itself to the government registry; see Government Deed Verification below for that.",
  },
  DEED_DUPLICATE: {
    label: 'Duplicate deed reference check',
    passedText: 'No duplicate deed reference text found on another live listing.',
    triggeredText: 'This deed reference text is already used by another live listing.',
  },
  SELLER_HISTORY: {
    label: 'Seller history check',
    passedText: "No concerning pattern in the seller's listing history.",
    triggeredText: "Seller has a history of rejected listings or resolved reports.",
  },
  LOCATION_INVALID: {
    label: 'Location validation',
    passedText: 'Listing coordinates were validated.',
    triggeredText: 'Location could not be validated against map coordinates.',
  },
  MISSING_INFO: {
    label: 'Missing listing information check',
    passedText: 'All required listing details are present.',
    triggeredText: 'Some required listing details are missing or too brief.',
  },
};

export function PropertyFraudPanel({
  riskLevel,
  riskScore,
  riskGeneratedDate,
  fraudReport,
  showRulePoints = false,
}: PropertyFraudPanelProps) {
  const attentionCount = fraudReport.filter((rule) => rule.triggered).length;

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
        Supporting Risk Indicators
      </Typography>

      <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', mb: 1.5, color: 'text.secondary' }}>
        <InfoOutlinedIcon fontSize="small" sx={{ mt: 0.25, flexShrink: 0 }} />
        <Typography variant="body2" color="text.secondary">
          These checks highlight listing-level concerns that may require review. They do not determine whether the
          deed is genuine. Government Deed Verification provides the authoritative deed comparison.
        </Typography>
      </Box>

      <Stack direction="row" spacing={0.75} sx={{ flexWrap: 'wrap', rowGap: 0.75 }}>
        <Chip label={`Legacy risk score: ${riskScore ?? '-'}/100`} size="small" variant="outlined" />
        <Chip label={`Risk level: ${riskLevel}`} size="small" variant="outlined" />
        <Chip
          label={attentionCount === 0 ? 'All indicators passed' : `${attentionCount} indicator${attentionCount === 1 ? '' : 's'} require attention`}
          size="small"
          color={attentionCount === 0 ? 'success' : 'warning'}
          variant="outlined"
        />
      </Stack>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
        Last assessed: {formatDate(riskGeneratedDate)}
      </Typography>

      {fraudReport.length > 0 && (
        <Stack spacing={1.25} sx={{ mt: 2 }}>
          {fraudReport.map((rule) => {
            const display = RULE_DISPLAY[rule.ruleCode];

            return (
              <Box key={rule.ruleCode}>
                <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 0.75 }}>
                  {rule.triggered ? (
                    <WarningAmberIcon fontSize="small" color="warning" sx={{ mt: 0.25 }} />
                  ) : (
                    <CheckCircleIcon fontSize="small" color="success" sx={{ mt: 0.25 }} />
                  )}
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body2">
                      {rule.triggered
                        ? (display?.triggeredText ?? `Review recommended: ${rule.ruleName}`)
                        : (display?.passedText ?? `${rule.ruleName}: passed`)}
                    </Typography>
                    {showRulePoints && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        {rule.ruleName} &middot; {rule.pointsAdded}/{rule.maxPoints} pts
                        {rule.description ? ` — ${rule.description}` : ''}
                      </Typography>
                    )}
                  </Box>
                </Box>
              </Box>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
}
