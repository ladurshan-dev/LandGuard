namespace LandGuard.Application.Common.Models;

/// <summary>
/// Result of saving an uploaded property photo via
/// <see cref="Interfaces.IFileStorageService"/>. <c>Url</c> is what gets
/// passed to <c>usp_PropertyImage_Add</c>'s <c>@ImageURL</c> parameter;
/// <c>Sha256Hash</c> is what gets passed to <c>@ImageHash</c> - the exact
/// fingerprint fraud rule 2 (Duplicate Image) compares across listings.
/// </summary>
public record StoredImageFile(string Url, string Sha256Hash);
