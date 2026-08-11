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

    /// <summary>
    /// The deed's registered owner name (explicit deed-owner data - see
    /// LandGuard.Domain.Entities.Property.OwnerName's own doc comment for
    /// why this is no longer substituted with the Seller account's Name).
    /// Null whenever the caller is neither this listing's owner nor an
    /// Admin - see OwnerNic's doc comment.
    /// </summary>
    public string? OwnerName { get; set; }

    /// <summary>
    /// The deed's registered owner NIC. Sensitive PII: null for a
    /// Buyer/anonymous/public caller viewing someone else's Approved
    /// listing, exactly like RiskScore - see PropertyService.
    /// RedactOwnerFields, the one place this is actually enforced.
    /// Non-null only for the owning Seller or an Admin.
    /// </summary>
    public string? OwnerNic { get; set; }

    /// <summary>The deed's registered owner address. Null for a Buyer/public caller - see OwnerNic's doc comment.</summary>
    public string? OwnerAddress { get; set; }

    /// <summary>"Pending" | "Approved" | "Flagged" | "Rejected".</summary>
    public string Status { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public string? SellerPhone { get; set; }

    public bool SellerNicVerified { get; set; }

    /// <summary>
    /// Null for a Buyer/anonymous/public caller viewing someone else's
    /// Approved listing - Buyer privacy requirement: fraud-engine output is
    /// internal, never exposed to the marketplace, even once a listing is
    /// Approved. Non-null only for the owning Seller or an Admin (see
    /// PropertyService.SearchAsync/GetByIdAsync's redaction logic, the one
    /// place this is actually enforced).
    /// </summary>
    public int? RiskScore { get; set; }

    /// <summary>"Low" | "Medium" | "High" - "Low" until the engine has run at least once. Null for a Buyer/public caller - see RiskScore's doc comment.</summary>
    public string? RiskLevel { get; set; }

    /// <summary>"Clean" | "Suspicious" | "Fraudulent" - "Clean" until the engine has run at least once. Null for a Buyer/public caller - see RiskScore's doc comment.</summary>
    public string? FraudStatus { get; set; }

    public string? RiskSummary { get; set; }

    public DateTime? RiskGeneratedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int ImageCount { get; set; }

    public int ReportCount { get; set; }
}
