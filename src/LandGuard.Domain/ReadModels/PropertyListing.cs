namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_PropertyListing</c> - property +
/// seller + risk badge + cover image, the main shape behind
/// <c>GET /api/properties</c> and <c>GET /api/properties/{id}</c>
/// (via <c>usp_Property_Search</c> / <c>usp_Property_GetById</c>).
/// </summary>
public class PropertyListing
{
    public int PropertyId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Location { get; set; } = null!;

    public string? District { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public double Size { get; set; }

    public decimal Price { get; set; }

    public decimal? PricePerPerch { get; set; }

    public string? DeedReference { get; set; }

    /// <summary>"Pending" | "Approved" | "Flagged" | "Rejected".</summary>
    public string Status { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public string? SellerPhone { get; set; }

    public bool SellerNicVerified { get; set; }

    public int? RiskScore { get; set; }

    /// <summary>"Low" | "Medium" | "High" - defaults to "Low" via ISNULL when no analysis has run yet.</summary>
    public string RiskLevel { get; set; } = null!;

    /// <summary>"Clean" | "Suspicious" | "Fraudulent" - defaults to "Clean" via ISNULL when no analysis has run yet.</summary>
    public string FraudStatus { get; set; } = null!;

    public string? RiskSummary { get; set; }

    public DateTime? RiskGeneratedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int ImageCount { get; set; }

    public int ReportCount { get; set; }
}
