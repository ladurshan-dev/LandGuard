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
 * decision, it just makes the four real backend statuses instantly
 * distinguishable at a glance (Pending under review, Approved and public,
 * Flagged for suspected fraud, Rejected).
 */
export function PropertyStatusChip({ status, size = 'small' }: PropertyStatusChipProps) {
  const color: ChipProps['color'] =
    status === 'Approved' ? 'success' : status === 'Flagged' ? 'error' : status === 'Rejected' ? 'default' : 'warning';

  return <Chip label={status} color={color} size={size} variant={status === 'Rejected' ? 'outlined' : 'filled'} />;
}
