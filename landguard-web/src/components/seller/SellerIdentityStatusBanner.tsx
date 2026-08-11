import { useState } from 'react';
import { Alert, AlertTitle, Box, Button, CircularProgress } from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useAuth } from '../../hooks/useAuth';
import { ApiError } from '../../utils/apiError';

/**
 * Seller Government Identity Verification requirement - shown on the
 * Seller dashboard and the properties list so a Pending/Failed Seller
 * always sees exactly why "List a Property" is unavailable, with a way to
 * retry. Renders nothing for a Verified Seller (or for anyone who isn't a
 * Seller) - callers don't need to check the role/status themselves before
 * using this.
 *
 * Deliberately never says "fraudulent" for a Failed identity - see
 * IdentityStatus's own doc comment: this is a statement about whether a
 * name/NIC could be matched against the Government Identity Registry,
 * never an accusation about the person.
 */
export function SellerIdentityStatusBanner() {
  const { user, reverifyIdentity } = useAuth();
  const [isRetrying, setIsRetrying] = useState(false);
  const [retryError, setRetryError] = useState<string | null>(null);

  if (!user || user.role !== 'Seller' || user.identityStatus === 'Verified') {
    return null;
  }

  const handleRetry = async () => {
    setIsRetrying(true);
    setRetryError(null);

    try {
      await reverifyIdentity();
    } catch (error) {
      setRetryError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsRetrying(false);
    }
  };

  const isFailed = user.identityStatus === 'Failed';

  return (
    <Alert
      severity={isFailed ? 'error' : 'warning'}
      sx={{ mb: 3 }}
      action={
        <Button
          color="inherit"
          size="small"
          onClick={() => void handleRetry()}
          disabled={isRetrying}
          startIcon={isRetrying ? <CircularProgress size={14} color="inherit" /> : <RefreshIcon fontSize="small" />}
        >
          {isRetrying ? 'Checking...' : 'Retry Identity Verification'}
        </Button>
      }
    >
      <AlertTitle>Government Identity {isFailed ? 'Verification Failed' : 'Pending'}</AlertTitle>
      {isFailed
        ? 'Your Name/NIC could not be verified against the Government Identity Registry.'
        : 'Identity verification could not be completed. Please try again.'}
      {' '}You cannot list a property until your identity is verified.
      {retryError && (
        <Box sx={{ mt: 1 }}>
          <Alert severity="error" sx={{ py: 0 }}>
            {retryError}
          </Alert>
        </Box>
      )}
    </Alert>
  );
}
