namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_SellerDashboard</c> - per-seller
/// listing counts and average risk score (FR08). One row per user with
/// <c>Role = 'Seller'</c>, including sellers with zero listings.
/// </summary>
public class SellerDashboard
{
    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public bool NicVerified { get; set; }

    public bool IsActive { get; set; }

    public int TotalListings { get; set; }

    public int ApprovedListings { get; set; }

    public int PendingListings { get; set; }

    public int FlaggedListings { get; set; }

    public int RejectedListings { get; set; }

    /// <summary>Null when the seller has no analysed listings yet.</summary>
    public decimal? AverageRiskScore { get; set; }
}
