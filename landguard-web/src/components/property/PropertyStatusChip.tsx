import { Chip } from '@mui/material';
import type { ChipProps } from '@mui/material';
import type { PropertyStatus } from '../../types/property';

interface PropertyStatusChipProps {
  status: PropertyStatus;
  size?: ChipProps['size'];
}

/**
 * Visual status indicator for a listing's dbo.Property.Status. Colour
 * mapping is purely presentational - it does not encode any authorization
 * decision, it just makes the six real backend statuses instantly
 * distinguishable at a glance (Pending under review, Approved and public,
 * Flagged for suspected fraud, Rejected, Withdrawn, Disapproved). Withdrawn
 * deliberately gets the same neutral/outlined treatment as Rejected, not
 * the amber "needs action" look Pending falls back to - it is a seller's
 * own lifecycle choice, not a fraud verdict, so it must not read as an
 * alarm. Disapproved (Mandatory Deed / Form-vs-Deed Verification
 * requirement - a SYSTEM-AUTOMATED outcome, distinct from a manual Admin
 * Rejected) gets the same filled "error" look as Flagged: unlike
 * Rejected/Withdrawn, it is a live problem the Seller needs to act on
 * (upload a corrected deed or fix the mismatched field and resubmit), not
 * settled history.
 */
export function PropertyStatusChip({ status, size = 'small' }: PropertyStatusChipProps) {
  const color: ChipProps['color'] =
    status === 'Approved'
      ? 'success'
      : status === 'Flagged' || status === 'Disapproved'
        ? 'error'
        : status === 'Rejected' || status === 'Withdrawn'
          ? 'default'
          : 'warning';

  const variant: ChipProps['variant'] = status === 'Rejected' || status === 'Withdrawn' ? 'outlined' : 'filled';

  return <Chip label={status} color={color} size={size} variant={variant} />;
}
