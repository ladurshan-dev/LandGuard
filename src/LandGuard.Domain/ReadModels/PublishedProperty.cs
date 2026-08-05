namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_PublishedProperty</c> - exactly
/// what a Buyer is allowed to see: approved listings from active sellers
/// only (<c>Status = 'Approved' AND Seller.IsActive = 1</c>). Same shape
/// as <see cref="PropertyListing"/> (the view is a <c>SELECT v.*</c> over
/// <c>vw_PropertyListing</c> with a filter) - kept as a distinct class
/// because EF Core maps one CLR type to exactly one database object, and
/// the distinction matters: querying this type can never leak an
/// unapproved or suspended-seller listing by accident, which querying
/// <see cref="PropertyListing"/> directly could.
/// </summary>
public class PublishedProperty
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

    public string Status { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public string? SellerPhone { get; set; }

    public bool SellerNicVerified { get; set; }

    public int? RiskScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string FraudStatus { get; set; } = null!;

    public string? RiskSummary { get; set; }

    public DateTime? RiskGeneratedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int ImageCount { get; set; }

    public int ReportCount { get; set; }
}
