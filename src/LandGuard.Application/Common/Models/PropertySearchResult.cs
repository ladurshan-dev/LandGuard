namespace LandGuard.Application.Common.Models;

/// <summary>
/// One row from <c>usp_Property_Search</c> - the same shape as
/// <see cref="PropertyListingResult"/> (the procedure selects from
/// <c>dbo.vw_PublishedProperty</c>, which is <c>vw_PropertyListing</c>
/// filtered to <c>Status = 'Approved'</c> and an active seller) plus
/// <c>TotalRecords</c>, which the procedure repeats on every row
/// specifically so a paged API response can read the grand total off
/// whichever row it likes without a second round trip.
/// </summary>
public class PropertySearchResult
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

    /// <summary>Null for a Buyer/public caller - see PropertyListingResult.OwnerNic's doc comment (the same redaction applies here via PropertyService.SearchAsync).</summary>
    public string? OwnerName { get; set; }

    /// <summary>Sensitive PII: null for a Buyer/public caller - see PropertyListingResult.OwnerNic's doc comment.</summary>
    public string? OwnerNic { get; set; }

    /// <summary>Null for a Buyer/public caller - see PropertyListingResult.OwnerNic's doc comment.</summary>
    public string? OwnerAddress { get; set; }

    public string Status { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public string? SellerPhone { get; set; }

    public bool SellerNicVerified { get; set; }

    /// <summary>
    /// Null for a Buyer/anonymous/public caller - Buyer privacy
    /// requirement: fraud-engine output is internal, never exposed to the
    /// marketplace, even for an Approved listing. Non-null only for an
    /// Admin caller (see PropertyService.SearchAsync's redaction logic).
    /// </summary>
    public int? RiskScore { get; set; }

    /// <summary>Null for a Buyer/public caller - see RiskScore's doc comment.</summary>
    public string? RiskLevel { get; set; }

    /// <summary>Null for a Buyer/public caller - see RiskScore's doc comment.</summary>
    public string? FraudStatus { get; set; }

    public string? RiskSummary { get; set; }

    public DateTime? RiskGeneratedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int ImageCount { get; set; }

    public int ReportCount { get; set; }

    /// <summary>Total rows matching the filter, ignoring paging - repeated on every row by the procedure.</summary>
    public int TotalRecords { get; set; }
}
