namespace LandGuard.Application.Common.Models;

/// <summary>
/// Shape returned by every Property stored procedure that does
/// <c>SELECT * FROM dbo.vw_PropertyListing</c> - <c>usp_Property_Create</c>,
/// <c>usp_Property_Update</c>, the first result set of
/// <c>usp_Property_GetById</c>, and every row of
/// <c>usp_Property_GetBySeller</c>. A dedicated read DTO rather than the
/// Domain <c>Property</c> entity or the EF Core-mapped
/// <c>PropertyListing</c>/<c>PublishedProperty</c> read models, following
/// the same reasoning Module 3 established for <c>NotificationSummary</c>:
/// this type only ever comes from a Dapper projection over a SQL view, not
/// a tracked EF Core query, and keeping it separate means a future change
/// to the EF Core read-model shape (e.g. for a LINQ-based report) can never
/// silently break this mapping or vice versa.
/// </summary>
public class PropertyListingResult
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

    /// <summary>"Low" | "Medium" | "High" - "Low" until the engine has run at least once.</summary>
    public string RiskLevel { get; set; } = null!;

    /// <summary>"Clean" | "Suspicious" | "Fraudulent" - "Clean" until the engine has run at least once.</summary>
    public string FraudStatus { get; set; } = null!;

    public string? RiskSummary { get; set; }

    public DateTime? RiskGeneratedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int ImageCount { get; set; }

    public int ReportCount { get; set; }
}
