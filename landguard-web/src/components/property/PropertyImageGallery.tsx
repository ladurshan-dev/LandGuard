import { useState } from 'react';
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  ImageList,
  ImageListItem,
  Tooltip,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import { resolveAssetUrl } from '../../api/axios';
import { PropertyImagePlaceholder } from './PropertyImagePlaceholder';
import type { PropertyImageSummary } from '../../types/property';

interface PropertyImageGalleryProps {
  images: PropertyImageSummary[];
  /**
   * Shows a delete button on each image and asks for confirmation before
   * calling this with the chosen ImageID. Omitted entirely on read-only
   * views (Buyer's property details page) - the same "kept out of this
   * shared component, wired in only where the caller actually owns the
   * property" split this component's doc comment already establishes for
   * upload. The real authorization boundary is the backend
   * (PropertyService.DeleteImageAsync), not this prop being present -
   * omitting it here is only so a Buyer never sees a control they'd get a
   * 403 from anyway.
   */
  onDeleteImage?: (imageId: number) => void;
  /** ImageID currently being deleted (shows a spinner on that image, disables its delete button) - undefined/null when nothing is in flight. */
  deletingImageId?: number | null;
}

/**
 * Read-only image grid for a property's uploaded photos, now optionally
 * with delete controls - used on every role's details page. Upload
 * remains a separate control kept fully out of this component; delete
 * follows the exact same "only present where the caller can actually use
 * it" split via the optional onDeleteImage prop, rather than this
 * component ever deciding for itself who's allowed to delete.
 */
export function PropertyImageGallery({ images, onDeleteImage, deletingImageId }: PropertyImageGalleryProps) {
  const [pendingDeleteImageId, setPendingDeleteImageId] = useState<number | null>(null);
  // Per-item broken-URL fallback (requirement: "if an image URL exists but
  // fails to load, gracefully switch to the same placeholder using
  // onError") - each of these is a real, uploaded PropertyImageSummary
  // record, so unlike the zero-images case above this is only ever
  // reached if a file went missing from disk after upload, not something
  // this component fabricates or hides the property for.
  const [brokenImageIds, setBrokenImageIds] = useState<ReadonlySet<number>>(new Set());

  if (images.length === 0) {
    // Same shared fallback tile PropertyCard now uses for a missing cover
    // image (see PropertyImagePlaceholder's own doc comment) - keeps the
    // "no photo" visual consistent across the card grid and this full
    // gallery, rather than this being the only place with a text-only
    // fallback. Buyer's property details page (the "handle zero images
    // gracefully" requirement) reaches this exact branch via
    // <PropertyImageGallery images={detail.images} /> with an empty array -
    // no separate fix was needed there.
    return <PropertyImagePlaceholder height={160} label="No images have been uploaded for this property yet." />;
  }

  const isOnlyImage = images.length === 1;

  const handleConfirm = () => {
    if (pendingDeleteImageId !== null) {
      onDeleteImage?.(pendingDeleteImageId);
      setPendingDeleteImageId(null);
    }
  };

  return (
    <>
      <ImageList cols={3} gap={8} sx={{ m: 0 }}>
        {images.map((image) => {
          const isDeletingThis = deletingImageId === image.imageId;
          const isBroken = brokenImageIds.has(image.imageId);

          return (
            <ImageListItem key={image.imageId} sx={{ position: 'relative' }}>
              {isBroken ? (
                <Box sx={{ aspectRatio: '4 / 3', borderRadius: 1, overflow: 'hidden' }}>
                  <PropertyImagePlaceholder height="100%" label="Image unavailable" />
                </Box>
              ) : (
                <img
                  src={resolveAssetUrl(image.imageUrl)}
                  alt="Property"
                  loading="lazy"
                  style={{ borderRadius: 4, aspectRatio: '4 / 3', objectFit: 'cover', opacity: isDeletingThis ? 0.5 : 1 }}
                  onError={() => setBrokenImageIds((prev) => new Set(prev).add(image.imageId))}
                />
              )}
              {image.isPrimary && (
                <Box sx={{ position: 'absolute', top: 4, left: 4 }}>
                  <Chip label="Primary" size="small" color="primary" />
                </Box>
              )}
              {onDeleteImage && (
                <Box sx={{ position: 'absolute', top: 4, right: 4 }}>
                  <Tooltip title="Delete image">
                    <span>
                      <IconButton
                        size="small"
                        sx={{ bgcolor: 'rgba(255,255,255,0.85)', '&:hover': { bgcolor: 'rgba(255,255,255,0.95)' } }}
                        disabled={deletingImageId != null}
                        onClick={() => setPendingDeleteImageId(image.imageId)}
                      >
                        {isDeletingThis ? <CircularProgress size={18} /> : <DeleteIcon fontSize="small" color="error" />}
                      </IconButton>
                    </span>
                  </Tooltip>
                </Box>
              )}
            </ImageListItem>
          );
        })}
      </ImageList>

      <Dialog open={pendingDeleteImageId !== null} onClose={() => setPendingDeleteImageId(null)}>
        <DialogTitle>Delete this image?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to delete this property image? This action cannot be undone.
          </DialogContentText>
          {isOnlyImage && (
            <DialogContentText sx={{ mt: 1.5, fontWeight: 600 }} color="warning.main">
              This is the only image on this property - deleting it will leave the property with no images.
            </DialogContentText>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingDeleteImageId(null)}>Cancel</Button>
          <Button color="error" variant="contained" onClick={handleConfirm}>
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
