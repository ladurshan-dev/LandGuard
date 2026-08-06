namespace LandGuard.Infrastructure.Services;

/// <summary>
/// Strongly-typed binding of the "FileStorage" configuration section,
/// bound via <c>services.Configure&lt;FileStorageSettings&gt;</c> and used
/// by <see cref="LocalFileStorageService"/>. Kept behind
/// <c>IFileStorageService</c> specifically so this local-disk
/// implementation (correct for this FYP's local SQL Server Express/IIS
/// Express deployment) can be swapped for Azure Blob Storage/S3 later by
/// adding one Infrastructure class and changing one DI registration.
/// </summary>
public class FileStorageSettings
{
    /// <summary>
    /// Filesystem path property photos are saved under, relative to the
    /// API project's content root unless rooted (e.g. "C:\...").
    /// </summary>
    public string RootPath { get; set; } = "wwwroot/uploads/properties";

    /// <summary>URL prefix the saved files are served under via <c>app.UseStaticFiles()</c>.</summary>
    public string PublicBaseUrl { get; set; } = "/uploads/properties";

    /// <summary>Maximum accepted upload size in bytes.</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = { "image/jpeg", "image/png", "image/webp" };
}
