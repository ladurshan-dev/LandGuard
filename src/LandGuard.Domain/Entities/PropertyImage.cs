namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.PropertyImage</c>. <see cref="ImageHash"/> is the
/// fingerprint (SHA-256/perceptual hash, produced by the API layer, not
/// the database) that fraud rule 2 (Duplicate Image) compares across
/// listings. Insert goes through <c>usp_PropertyImage_Add</c>.
/// </summary>
public class PropertyImage
{
    public int ImageId { get; set; }

    public int PropertyId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? ImageHash { get; set; }

    /// <summary>[ext] Cover/thumbnail flag.</summary>
    public bool IsPrimary { get; set; }

    public DateTime UploadedDate { get; set; }

    public Property Property { get; set; } = null!;
}
