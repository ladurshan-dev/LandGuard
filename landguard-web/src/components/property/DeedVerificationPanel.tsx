import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, CircularProgress, Paper, Typography } from '@mui/material';
import GavelIcon from '@mui/icons-material/Gavel';
import { DeedVerificationResultView } from './DeedVerificationResultView';
import { getDeedVerificationHistory } from '../../services/deedVerificationService';
import { ApiError } from '../../utils/apiError';
import type { DeedVerificationResponse } from '../../types/deedVerification';

interface DeedVerificationPanelProps {
  propertyId: number;
}

/**
 * Government Deed Verification, Admin's read-only review view (Phase D).
 * Previously this panel let an Admin upload a second deed and run a
 * brand-new, session-only verification here - that workflow is retired:
 * the Seller now uploads their deed as part of listing the property
 * (PropertyFormPage) or from their own property page
 * (SellerDeedVerificationSection), and GovernmentDeedVerificationService
 * persists the result. This panel only reads that persisted result back
 * (GET /api/deed-verification/{propertyId} - newest run first) - an Admin
 * is never asked to upload a deed here.
 *
 * The seller's uploaded document itself is not fetchable/viewable from
 * here: no controller in this solution streams a stored document back over
 * HTTP yet (IFileStorageService.OpenDocumentAsync is server-side only,
 * used to re-OCR the trusted government PDF, never to serve a file to a
 * browser), and building that safely is out of scope for this phase. When
 * a seller document reference is present, DeedVerificationResultView
 * already reports "Seller deed uploaded and verified" as plain text
 * instead of offering a View/Open action - secure document download is
 * left for a later phase.
 */
export function DeedVerificationPanel({ propertyId }: DeedVerificationPanelProps) {
  const [history, setHistory] = useState<DeedVerificationResponse[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

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

  const current = history && history.length > 0 ? history[0] : null;

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <GavelIcon fontSize="small" color="primary" />
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Government Deed Verification
        </Typography>
      </Box>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Authoritative evidence of deed authenticity, checked directly against the government land registry when the
        seller listed this property. This is separate from the fraud/risk indicators above - a Low risk score does
        not mean a deed is verified, and a High risk score does not mean it is fraudulent.
      </Typography>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
          <CircularProgress size={28} />
        </Box>
      )}

      {!isLoading && loadError && <Alert severity="error">{loadError}</Alert>}

      {!isLoading && !loadError && current && <DeedVerificationResultView result={current} />}

      {!isLoading && !loadError && !current && (
        <Alert severity="info">
          Deed verification has not been completed. The seller has not yet uploaded and verified a deed document for
          this property.
        </Alert>
      )}
    </Paper>
  );
}
