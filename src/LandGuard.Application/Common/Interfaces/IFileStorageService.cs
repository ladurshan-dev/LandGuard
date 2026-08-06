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
}
