import { Box, LinearProgress, Paper, Stack, Typography } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { RiskIndicator } from './RiskIndicator';
import { formatDate } from '../../utils/format';
import type { FraudStatus, PropertyFraudRuleResult, RiskLevel } from '../../types/property';

interface PropertyFraudPanelProps {
  riskLevel: RiskLevel;
  fraudStatus: FraudStatus;
  riskScore: number | null;
  riskSummary: string | null;
  riskGeneratedDate: string | null;
  fraudReport: PropertyFraudRuleResult[];
}

/**
 * Renders the fraud engine's output for one property (score, level,
 * summary, and the per-rule breakdown) - shared by every role's details
 * page, since PropertyDetail.fraudReport is the exact same shape
 * regardless of who's viewing it. Never lets the frontend approve/reject/
 * flag anything; this is read-only reporting of what
 * usp_Fraud_AnalyseProperty already decided.
 */
export function PropertyFraudPanel({
  riskLevel,
  fraudStatus,
  riskScore,
  riskSummary,
  riskGeneratedDate,
  fraudReport,
}: PropertyFraudPanelProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
        Fraud &amp; Risk Assessment
      </Typography>

      <RiskIndicator riskLevel={riskLevel} fraudStatus={fraudStatus} riskScore={riskScore} size="medium" />

      {riskSummary && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
          {riskSummary}
        </Typography>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
        Last assessed: {formatDate(riskGeneratedDate)}
      </Typography>

      {fraudReport.length > 0 && (
        <Stack spacing={1.5} sx={{ mt: 2 }}>
          {fraudReport.map((rule) => (
            <Box key={rule.ruleCode}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 1 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                  {rule.triggered ? (
                    <WarningAmberIcon fontSize="small" color="error" />
                  ) : (
                    <CheckCircleIcon fontSize="small" color="success" />
                  )}
                  <Typography variant="body2">{rule.ruleName}</Typography>
                </Box>
                <Typography variant="caption" color="text.secondary">
                  {rule.pointsAdded}/{rule.maxPoints} pts
                </Typography>
              </Box>
              <LinearProgress
                variant="determinate"
                value={rule.maxPoints > 0 ? (rule.pointsAdded / rule.maxPoints) * 100 : 0}
                color={rule.triggered ? 'error' : 'success'}
                sx={{ mt: 0.5, borderRadius: 1 }}
              />
              {rule.description && (
                <Typography variant="caption" color="text.secondary">
                  {rule.description}
                </Typography>
              )}
            </Box>
          ))}
        </Stack>
      )}
    </Paper>
  );
}
