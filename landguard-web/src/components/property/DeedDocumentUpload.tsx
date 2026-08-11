import { useRef, useState } from 'react';
import type { ChangeEvent, DragEvent } from 'react';
import { Box, IconButton, Stack, Typography } from '@mui/material';
import DescriptionIcon from '@mui/icons-material/Description';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import CloseIcon from '@mui/icons-material/Close';

/**
 * Reused by PropertyFormPage (Phase D - required on create) and
 * SellerPropertyDetailsPage (Phase D - optional retry/re-verify), so the
 * dashed drop zone, size formatting and remove/change affordance stay
 * identical in both places instead of drifting between two copies. Purely
 * presentational: it only ever hands the browser File back to its caller
 * via onFileSelected - it does not call deedVerificationService itself,
 * matching every other *Panel/*Upload component in this folder (e.g.
 * DeedVerificationPanel) that leaves the actual API call to its caller.
 *
 * PDF-ONLY CORRECTION (manual-testing fix, confirmed root cause via a
 * controlled `accept=".pdf,application/pdf"` test that immediately showed
 * every PDF in the native Windows file picker): the deed document is now
 * PDF-only, both here and in GovernmentDeedComparisonService.CompareAsync
 * on the backend (that method's own PDF-ONLY CORRECTION comment). This
 * component previously also accepted image/jpeg, image/png, image/tiff
 * (OcrValidationRules' broader set, which still backs the generic, UI-unused
 * POST /api/ocr/extract endpoint) - that is no longer offered here, and a
 * selected/dropped non-PDF file is rejected client-side below rather than
 * only relying on the `accept` attribute (which is a picker hint only, not
 * enforcement - drag-and-drop bypasses it entirely).
 *
 * This constant, and the validation using it, are shared, unchanged, by
 * both call sites (PropertyFormPage's create-time upload and
 * SellerDeedVerificationSection's Replace/Re-verify Deed) - there is only
 * ever one deed input. Property IMAGE upload (SellerPropertyDetailsPage's
 * separate, unrelated <input accept="image/jpeg,image/png,image/webp">) is
 * untouched.
 */
const ACCEPTED_EXTENSIONS = '.pdf,application/pdf';

/**
 * True MIME-type check first (`file.type === 'application/pdf'`, what the
 * browser itself determined from the file's actual content/registration,
 * not just its name). Falls back to the filename extension only when the
 * browser reports no/an unhelpful MIME type (some Windows file-manager/
 * browser combinations do this for an otherwise-legitimate PDF) - a
 * genuine non-PDF renamed to end in ".pdf" would still be caught by the
 * backend's own content-type check either way (defense in depth, the same
 * principle every *RequestValidator in this codebase already follows
 * alongside its own stored-procedure/service-layer re-check).
 */
function isPdfFile(file: File): boolean {
  if (file.type === 'application/pdf') {
    return true;
  }
  return file.name.toLowerCase().endsWith('.pdf');
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface DeedDocumentUploadProps {
  selectedFile: File | null;
  onFileSelected: (file: File) => void;
  onRemove: () => void;
  disabled?: boolean;
  error?: string | null;
  title?: string;
  description?: string;
}

export function DeedDocumentUpload({
  selectedFile,
  onFileSelected,
  onRemove,
  disabled = false,
  error = null,
  title = 'Land Deed Document',
  description = 'Upload the deed document that proves ownership of this property. LandGuard will compare it with the Government Registry before an administrator reviews the listing.',
}: DeedDocumentUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragActive, setIsDragActive] = useState(false);
  // Client-side PDF-only rejection (manual-testing fix) - the `accept`
  // attribute is a picker hint only; it does not stop a non-PDF file
  // dropped via drag-and-drop, which never goes through the file input's
  // own filtering at all. Takes priority over the parent-supplied `error`
  // prop (a fresh, obviously-invalid selection is more relevant than a
  // stale server-side error from a previous attempt), cleared as soon as a
  // valid PDF is chosen or the current selection is removed.
  const [localError, setLocalError] = useState<string | null>(null);

  const handleInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      if (isPdfFile(file)) {
        setLocalError(null);
        onFileSelected(file);
      } else {
        setLocalError('Only PDF files are accepted for the deed document.');
      }
    }
    // Allows re-selecting the exact same file after Remove, which a bare
    // input's own change event would otherwise silently ignore.
    event.target.value = '';
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragActive(false);
    if (disabled) {
      return;
    }
    const file = event.dataTransfer.files?.[0];
    if (file) {
      if (isPdfFile(file)) {
        setLocalError(null);
        onFileSelected(file);
      } else {
        setLocalError('Only PDF files are accepted for the deed document.');
      }
    }
  };

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
        {title}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5 }}>
        {description}
      </Typography>

      {!selectedFile && (
        <Box
          role="button"
          tabIndex={disabled ? -1 : 0}
          onClick={() => !disabled && inputRef.current?.click()}
          onKeyDown={(event) => {
            if (!disabled && (event.key === 'Enter' || event.key === ' ')) {
              inputRef.current?.click();
            }
          }}
          onDragOver={(event) => {
            event.preventDefault();
            if (!disabled) {
              setIsDragActive(true);
            }
          }}
          onDragLeave={() => setIsDragActive(false)}
          onDrop={handleDrop}
          sx={{
            border: '2px dashed',
            borderColor: isDragActive ? 'primary.main' : 'divider',
            borderRadius: 2,
            bgcolor: isDragActive ? 'primary.light' : 'background.default',
            opacity: isDragActive ? 0.92 : 1,
            py: 4,
            px: 2,
            textAlign: 'center',
            cursor: disabled ? 'not-allowed' : 'pointer',
            transition: 'border-color 0.15s ease, background-color 0.15s ease',
            '&:hover': disabled ? undefined : { borderColor: 'primary.main' },
          }}
        >
          <UploadFileIcon sx={{ fontSize: 36, color: 'primary.main', mb: 1 }} />
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Upload Deed
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            Drag and drop, or click to browse &middot; PDF only
          </Typography>
          <input
            ref={inputRef}
            type="file"
            hidden
            accept={ACCEPTED_EXTENSIONS}
            disabled={disabled}
            onChange={handleInputChange}
          />
        </Box>
      )}

      {selectedFile && (
        <Stack
          direction="row"
          spacing={1.5}
          sx={{
            alignItems: 'center',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 2,
            bgcolor: 'background.default',
            p: 1.5,
          }}
        >
          <DescriptionIcon sx={{ color: 'primary.main' }} />
          <Box sx={{ minWidth: 0, flexGrow: 1 }}>
            <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap title={selectedFile.name}>
              {selectedFile.name}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatFileSize(selectedFile.size)}
            </Typography>
          </Box>
          <IconButton
            size="small"
            onClick={() => {
              setLocalError(null);
              onRemove();
            }}
            disabled={disabled}
            aria-label="Remove selected deed document"
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Stack>
      )}

      {(localError ?? error) && (
        <Typography variant="caption" color="error" sx={{ display: 'block', mt: 1 }}>
          {localError ?? error}
        </Typography>
      )}
    </Box>
  );
}
