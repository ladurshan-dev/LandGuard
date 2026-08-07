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

    // ---- Module 5B (OCR Integration) additions below -----------------
    // Kept on this same settings class rather than a new one, since it's
    // still "where do uploaded files live" - but every field is new and
    // additive; nothing above this line changed.

    /// <summary>
    /// Filesystem path uploaded source documents (deed PDFs/scans for OCR)
    /// are saved under, relative to the API project's content root unless
    /// rooted. Deliberately OUTSIDE <see cref="RootPath"/>/wwwroot: unlike
    /// property photos, these can contain personal identity information
    /// (NIC, address) and must not be reachable through
    /// <c>app.UseStaticFiles()</c>'s unauthenticated static-file pipeline.
    /// There is no authenticated retrieval endpoint for them yet either -
    /// see <c>LocalFileStorageService.SaveDocumentAsync</c>'s doc comment.
    /// </summary>
    public string DocumentsRootPath { get; set; } = "App_Data/uploads/documents";

    /// <summary>Larger than MaxFileSizeBytes - deed scans/PDFs run bigger than a single property photo.</summary>
    public long MaxDocumentSizeBytes { get; set; } = 15 * 1024 * 1024;

    public string[] AllowedDocumentContentTypes { get; set; } =
        { "application/pdf", "image/jpeg", "image/png", "image/tiff" };
}
