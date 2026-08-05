namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_BuyerSavedProperty</c> - a
/// buyer's saved listings joined with their current risk (FR07). Backs
/// <c>usp_SavedProperty_GetByBuyer</c>.
/// </summary>
public class BuyerSavedProperty
{
    public int SavedPropertyId { get; set; }

    public int BuyerId { get; set; }

    public DateTime SavedDate { get; set; }

    public int PropertyId { get; set; }

    public string Title { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string? District { get; set; }

    public decimal Price { get; set; }

    public double Size { get; set; }

    public string Status { get; set; } = null!;

    public int? RiskScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string? CoverImageUrl { get; set; }

    public string SellerName { get; set; } = null!;
}
