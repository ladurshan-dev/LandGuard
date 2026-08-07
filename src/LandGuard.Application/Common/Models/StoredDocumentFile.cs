namespace LandGuard.Application.Common.Models;

/// <summary>
/// Result of saving an uploaded source document (a land deed PDF/scan for
/// OCR - Module 5B) via <c>IFileStorageService.SaveDocumentAsync</c>.
/// Deliberately not called "Url" like <see cref="StoredImageFile"/>:
/// documents are stored outside the publicly-servable wwwroot tree (see
/// <c>FileStorageSettings.DocumentsRootPath</c>) because they may contain
/// personal identity information (NIC, address) that property photos
/// never do, so <see cref="StorageReference"/> is a storage key an
/// authenticated retrieval endpoint could resolve later - not yet a
/// working public URL.
/// </summary>
public record StoredDocumentFile(string StorageReference, string Sha256Hash);
