namespace LandGuard.Application.DTOs.Property;

/// <summary>
/// GET /api/properties (FR10) - bound from query string. Mirrors
/// usp_Property_Search's parameters exactly; PageNumber/PageSize are left
/// unvalidated on purpose (see PropertySearchRequestValidator) because the
/// procedure itself already clamps them defensively (1-100), and mirroring
/// a forgiving default there is preferable to a 400 for a slightly odd
/// page size.
/// </summary>
public class PropertySearchRequest
{
    public string? Keyword { get; set; }

    public string? District { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public double? MinSize { get; set; }

    public double? MaxSize { get; set; }

    /// <summary>"Low" | "Medium" | "High".</summary>
    public string? RiskLevel { get; set; }

    /// <summary>"Newest" (default) | "PriceAsc" | "PriceDesc" | "RiskAsc".</summary>
    public string SortBy { get; set; } = "Newest";

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}
