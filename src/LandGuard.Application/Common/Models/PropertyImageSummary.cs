namespace LandGuard.Application.Common.Models;

/// <summary>
/// One row of the second result set of <c>usp_Property_GetById</c>
/// (ImageID, ImageURL, ImageHash, IsPrimary, UploadedDate - notably no
/// <c>PropertyID</c>, since the caller already knows it).
/// </summary>
public class PropertyImageSummary
{
    public int ImageId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? ImageHash { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime UploadedDate { get; set; }
}
