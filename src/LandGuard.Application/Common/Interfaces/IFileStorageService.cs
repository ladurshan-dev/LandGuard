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
    /// Saves a trusted government deed document (Government Registry
    /// module, Phase 3) and computes its SHA-256 fingerprint, following the
    /// exact same generated-filename/hashing mechanism as
    /// <see cref="SaveDocumentAsync"/>. Scoped by
    /// <paramref name="recordId"/> - a <c>GovernmentLandRecordDto.RecordId</c>
    /// business key such as "GR-000001" - not by a seller/user id: this
    /// document was never uploaded by any LandGuard account, so reusing
    /// <see cref="SaveDocumentAsync"/>'s <c>uploadedByUserId</c> parameter
    /// would mean inventing a fake one. Added alongside
    /// <see cref="SaveDocumentAsync"/> rather than a second file-storage
    /// service, per the Government Registry module's explicit "reuse the
    /// existing local file storage service... do not duplicate"
    /// instruction - purely additive, no change to
    /// <see cref="SaveImageAsync"/>'s or <see cref="SaveDocumentAsync"/>'s
    /// behavior or callers.
    /// </summary>
    Task<StoredDocumentFile> SaveGovernmentDocumentAsync(
        string recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a previously-saved document (seller or government) for
    /// reading, given the exact <c>StorageReference</c>
    /// <see cref="SaveDocumentAsync"/> or <see cref="SaveGovernmentDocumentAsync"/>
    /// returned - the one read-side counterpart both of those write-only
    /// methods have lacked since Module 5B/Government Registry Phase 3
    /// (see those methods' doc comments; this is that deferred follow-up,
    /// now needed by <c>GovernmentDeedComparisonService</c>, Phase 4, to
    /// re-OCR the trusted government deed). Deliberately generic over
    /// <em>which</em> kind of document reference is passed in - both
    /// save methods produce the same <c>"documents/..."</c>-prefixed
    /// logical shape, so one read method safely serves both rather than
    /// needing a second, government-specific one.
    ///
    /// Returns null - never throws - when
    /// <paramref name="storageReference"/> doesn't resolve to a real file
    /// under this service's configured storage root (unrecognised prefix,
    /// path-traversal attempt, or a reference whose file no longer
    /// exists): "the document isn't available" is an expected, valid
    /// outcome here (see Government Scenario F - a record with no PDF on
    /// file), not an exceptional one. Never resolves outside
    /// <c>FileStorageSettings.DocumentsRootPath</c>, regardless of what a
    /// malformed or tampered <paramref name="storageReference"/> contains.
    /// The caller owns disposing the returned stream.
    /// </summary>
    Task<Stream?> OpenDocumentAsync(string storageReference, CancellationToken cancellationToken = default);
}
