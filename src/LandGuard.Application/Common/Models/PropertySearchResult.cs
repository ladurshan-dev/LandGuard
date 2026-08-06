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

    /// <summary>Total rows matching the filter, ignoring paging - repeated on every row by the procedure.</summary>
    public int TotalRecords { get; set; }
}
