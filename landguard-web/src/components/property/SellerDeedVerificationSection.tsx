import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, Button, CircularProgress, Divider, Paper, Typography } from '@mui/material';
import GavelIcon from '@mui/icons-material/Gavel';
import { DeedDocumentUpload } from './DeedDocumentUpload';
import { DeedVerificationResultView } from './DeedVerificationResultView';
import { getDeedVerificationHistory, verifyDeed } from '../../services/deedVerificationService';
import { ApiError } from '../../utils/apiError';
import type { DeedVerificationResponse } from '../../types/deedVerification';

interface SellerDeedVerificationSectionProps {
  propertyId: number;
  /**
   * Called once after a verify/re-verify POST succeeds, alongside this
   * component's own loadHistory() refresh - lets the parent
   * (SellerPropertyDetailsPage) re-fetch PropertyDetail so its status chip
   * and any status-dependent messaging reflect the new Property.Status
   * (Approved/Pending/Disapproved) immediately, without the seller having
   * to reload the page. Optional and a no-op if omitted, so this component
   * still works standalone.
   */
  onVerified?: () => void;
}

/**
 * The Seller's own Deed Verification section (Phase D) -
 * SellerPropertyDetailsPage's counterpart to the Admin's read-only review
 * panel. Reads back whatever GovernmentDeedVerificationService has already
 * persisted (GET /api/deed-verification/{propertyId} - newest run first)
 * instead of forcing a fresh verification on every page load. When no
 * verification exists yet - whether because the seller hasn't uploaded a
 * deed, or because upload/verification failed right after property
 * creation (see PropertyFormPage's onSubmit, which navigates here with
 * `state.deedVerificationFailed` in that case) - the exact same "Upload /
 * Verify Deed" control below doubles as the retry path, per this phase's
 * explicit "allow retrying deed upload from Seller Property Details"
 * instruction. Once a verification exists, it is shown as-is; re-running
 * it is an explicit, separate action ("Replace / Re-verify Deed"), never
 * automatic, and never destroys the past record (PersistAsync is
 * append-only - see the backend's own DeedVerification entity doc
 * comment).
 */
export function SellerDeedVerificationSection({ propertyId, onVerified }: SellerDeedVerificationSectionProps) {
  const [history, setHistory] = useState<DeedVerificationResponse[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [isUploadFormOpen, setIsUploadFormOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState<string | null>(null);

  const loadHistory = useCallback(() => {
    getDeedVerificationHistory(propertyId)
      .then((result) => {
        setHistory(result);
        setLoadError(null);
      })
      .catch((error: unknown) => {
        setLoadError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [propertyId]);

  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  const handleVerify = async () => {
    if (!selectedFile) {
      return;
    }

    setIsVerifying(true);
    setVerifyError(null);

    try {
      await verifyDeed(propertyId, selectedFile);
      setSelectedFile(null);
      setIsUploadFormOpen(false);
      setIsLoading(true);
      loadHistory();
      // Property.Status may have just changed (Approved/Pending/Disapproved
      // - see usp_Property_ApplyDeedVerificationOutcome/
      // usp_Property_MarkPendingForReverification) as a side effect of the
      // POST above, entirely independent of this component's own history
      // state - let the parent re-fetch PropertyDetail so its status chip
      // reflects that immediately too.
      onVerified?.();
    } catch (error) {
      setVerifyError(error instanceof ApiError ? error.message : 'Something went wrong. Please try again.');
    } finally {
      setIsVerifying(false);
    }
  };

  const current = history && history.length > 0 ? history[0] : null;
  const showUploadForm = isUploadFormOpen || !current;

  return (
    <Paper variant="outlined" sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <GavelIcon fontSize="small" color="primary" />
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Government Deed Verification
        </Typography>
      </Box>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Authoritative evidence of deed authenticity, checked directly against the government land registry - separate
        from the fraud/risk indicators above.
      </Typography>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
          <CircularProgress size={28} />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && (
        <>
          {current ? (
            <>
              <DeedVerificationResultView result={current} />
              {!isUploadFormOpen && (
                <Button size="small" sx={{ mt: 2 }} onClick={() => setIsUploadFormOpen(true)}>
                  Replace / Re-verify Deed
                </Button>
              )}
            </>
          ) : (
            <Alert severity="info" sx={{ mb: 2 }}>
              Deed verification has not been completed.
            </Alert>
          )}

          {showUploadForm && (
            <Box sx={{ mt: current ? 2.5 : 0 }}>
              {current && <Divider sx={{ mb: 2.5 }} />}

              <DeedDocumentUpload
                selectedFile={selectedFile}
                onFileSelected={(file) => {
                  setSelectedFile(file);
                  setVerifyError(null);
                }}
                onRemove={() => setSelectedFile(null)}
                disabled={isVerifying}
                error={verifyError}
                title={current ? 'Replace Deed Document' : 'Land Deed Document'}
                description={
                  current
                    ? 'Upload a new deed document to run verification again. The previous result stays on record.'
                    : 'Upload the deed document that proves ownership of this property. LandGuard will compare it with the Government Registry before an administrator reviews the listing.'
                }
              />

              <Box sx={{ display: 'flex', gap: 1.5, mt: 2 }}>
                <Button
                  variant="contained"
                  onClick={() => void handleVerify()}
                  disabled={!selectedFile || isVerifying}
                  startIcon={isVerifying ? <CircularProgress size={16} color="inherit" /> : undefined}
                >
                  {isVerifying ? 'Verifying...' : 'Verify Deed'}
                </Button>
                {current && (
                  <Button
                    variant="text"
                    disabled={isVerifying}
                    onClick={() => {
                      setIsUploadFormOpen(false);
                      setSelectedFile(null);
                      setVerifyError(null);
                    }}
                  >
                    Cancel
                  </Button>
                )}
              </Box>
            </Box>
          )}
        </>
      )}
    </Paper>
  );
}
