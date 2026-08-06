namespace LandGuard.Application.DTOs.Property.Validators;

/// <summary>
/// Shared validation constants for the Property DTO validators, mirroring
/// the exact constraints <c>Database/Scripts/01_Schema.sql</c> enforces on
/// <c>dbo.Property</c> so a request that fails validation here would also
/// have failed the database's own CHECK constraints/column widths - this
/// just surfaces that as a clear 400 before a round trip to SQL.
/// </summary>
internal static class PropertyValidationRules
{
    public const int TitleMaxLength = 200;        // Property.Title NVARCHAR(200)
    public const int LocationMaxLength = 255;      // Property.Location NVARCHAR(255)
    public const int DistrictMaxLength = 100;      // Property.District NVARCHAR(100)
    public const int DeedReferenceMaxLength = 100; // Property.DeedReference VARCHAR(100)

    /// <summary>Not a DB constraint - a defensive application-level cap on free-text Description length.</summary>
    public const int DescriptionMaxLength = 4000;

    public static readonly string[] ValidRiskLevels = { "Low", "Medium", "High" };

    public static readonly string[] ValidSortOptions = { "Newest", "PriceAsc", "PriceDesc", "RiskAsc" };

    /// <summary>
    /// Mirrors Infrastructure/Services/FileStorageSettings' default -
    /// duplicated here (rather than Application referencing an
    /// Infrastructure type) so PropertyService can reject an oversized/
    /// wrong-type upload before ever touching IFileStorageService, with no
    /// dependency on ASP.NET Core's IFormFile.
    /// </summary>
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp" };
}
