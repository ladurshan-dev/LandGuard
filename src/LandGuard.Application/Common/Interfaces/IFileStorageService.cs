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
}
