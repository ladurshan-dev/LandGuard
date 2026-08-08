using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over saving an uploaded property photo and computing the
/// fingerprint fraud rule 2 (Duplicate Image) compares. PropertyService
/// depends on this, not on the filesystem or any cloud SDK directly, so
/// local disk storage (Infrastructure's <c>LocalFileStorageService</c>,
/// used for this FYP's local SQL Server Express deployment) can be
/// swapped for Azure Blob Storage/S3 later by adding one Infrastructure
/// class, with zero change to PropertyService.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves <paramref name="content"/> under a path scoped to
    /// <paramref name="propertyId"/> and returns the URL to store in
    /// <c>PropertyImage.ImageURL</c> plus the SHA-256 hash to store in
    /// <c>PropertyImage.ImageHash</c>.
    /// </summary>
    Task<StoredImageFile> SaveImageAsync(
        int propertyId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a general document upload (a land deed PDF/scan for OCR -
    /// Module 5B) and computes its SHA-256 fingerprint, scoped by the
    /// uploading user rather than a property, since OCR extraction can run
    /// before any property exists. Added alongside <see cref="SaveImageAsync"/>
    /// rather than a second file-storage service, per Module 5B's explicit
    /// "reuse the existing local file storage service... do not duplicate"
    /// instruction - purely additive, no change to SaveImageAsync's
    /// behavior or callers (PropertyService, Module 4).
    /// </summary>
    Task<StoredDocumentFile> SaveDocumentAsync(
        int uploadedByUserId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously-saved property image from disk, given the
    /// exact URL <see cref="SaveImageAsync"/> returned (and
    /// <c>PropertyImage.ImageURL</c> stores). Safe to call when the
    /// file is already missing, when <paramref name="imageUrl"/> doesn't
    /// resolve to anything under this service's configured storage root,
    /// or on any filesystem error - all of those are treated as a no-op
    /// rather than a thrown exception, since the caller's real intent
    /// ("this image should no longer exist on disk") is already
    /// satisfied and the <c>PropertyImage</c> database row remains the
    /// authoritative record either way. Never deletes anything outside
    /// <c>FileStorageSettings.RootPath</c>, regardless of what a
    /// malformed or tampered <paramref name="imageUrl"/> contains.
    /// </summary>
    Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}
