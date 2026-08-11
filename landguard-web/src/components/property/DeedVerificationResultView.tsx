import { Box, Chip, Stack, Typography } from '@mui/material';
import type { ChipProps } from '@mui/material';
import { formatDate } from '../../utils/format';
import type { DeedVerificationResponse, DeedVerificationStatus } from '../../types/deedVerification';

/**
 * Renders one persisted Government Deed Verification result - the status
 * chip, government record reference, summary and per-field evidence.
 * Shared by SellerPropertyDetailsPage's own Deed Verification section and
 * the Admin review panel (DeedVerificationPanel) so both surfaces present
 * the identical persisted record identically, rather than two independent
 * copies of this formatting drifting apart. Purely presentational - takes
 * an already-fetched DeedVerificationResponse (from either the POST verify
 * response or a GET history entry, which are field-for-field identical -
 * see the backend's DeedVerificationResponse.FromHistoryEntry) and fetches
 * nothing itself.
 */
const STATUS_COLOR: Record<DeedVerificationStatus, ChipProps['color']> = {
  Verified: 'success',
  Fraudulent: 'error',
  PriceAnomaly: 'warning',
  Unverified: 'default',
  UnverifiedCancelled: 'default',
  FormMismatch: 'error',
};

const STATUS_LABEL: Record<DeedVerificationStatus, string> = {
  Verified: 'Verified against government registry',
  Fraudulent: 'Fraudulent - material mismatch found',
  PriceAnomaly: 'Price anomaly',
  Unverified: 'Unverified - no matching government record',
  UnverifiedCancelled: 'Unverified - government record cancelled',
  FormMismatch: 'Listing information does not match the uploaded deed',
};

interface DeedVerificationResultViewProps {
  result: DeedVerificationResponse;
}

export function DeedVerificationResultView({ result }: DeedVerificationResultViewProps) {
  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
        <Chip label={STATUS_LABEL[result.verificationStatus]} color={STATUS_COLOR[result.verificationStatus]} />
        {result.governmentRecordId && (
          <Typography variant="caption" color="text.secondary">
            Government record: {result.governmentRecordId}
            {result.governmentRecordStatus ? ` (${result.governmentRecordStatus})` : ''}
          </Typography>
        )}
      </Box>

      <Typography variant="body2" sx={{ mt: 1.5 }}>
        {result.summary}
      </Typography>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
        Verified: {formatDate(result.generatedDate)}
        {result.sellerDocumentReference ? ' · Seller deed uploaded and verified' : ''}
      </Typography>

      {result.evidence.length > 0 && (
        <Stack spacing={1} sx={{ mt: 2 }}>
          {result.evidence.map((field) => (
            <Box key={field.fieldName} sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
              <Typography variant="body2" sx={{ fontWeight: 600, minWidth: 120 }}>
                {field.fieldName}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ flexGrow: 1 }}>
                {field.message}
              </Typography>
              <Chip label={field.match ? 'Match' : 'Mismatch'} color={field.match ? 'success' : 'error'} size="small" />
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
