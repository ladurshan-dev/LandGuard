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
 * decision, it just makes the five real backend statuses instantly
 * distinguishable at a glance (Pending under review, Approved and public,
 * Flagged for suspected fraud, Rejected, Withdrawn). Withdrawn deliberately
 * gets the same neutral/outlined treatment as Rejected, not the amber
 * "needs action" look Pending falls back to - it is a seller's own
 * lifecycle choice, not a fraud verdict, so it must not read as an alarm.
 */
export function PropertyStatusChip({ status, size = 'small' }: PropertyStatusChipProps) {
  const color: ChipProps['color'] =
    status === 'Approved'
      ? 'success'
      : status === 'Flagged'
        ? 'error'
        : status === 'Rejected' || status === 'Withdrawn'
          ? 'default'
          : 'warning';

  const variant: ChipProps['variant'] = status === 'Rejected' || status === 'Withdrawn' ? 'outlined' : 'filled';

  return <Chip label={status} color={color} size={size} variant={variant} />;
}
