namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless read model mapped to <c>dbo.vw_FlaggedProperty</c> - the admin
/// review queue (<c>GET /api/admin/flagged</c> via
/// <c>usp_Admin_GetFlagged</c>). Includes any listing that is
/// Flagged/Pending, or that has an open suspicious report, even if its
/// own status is currently Approved.
/// </summary>
public class FlaggedProperty
{
    public int PropertyId { get; set; }

    public string Title { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string? District { get; set; }

    public decimal Price { get; set; }

    public double Size { get; set; }

    public string? DeedReference { get; set; }

    public string Status { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public int SellerId { get; set; }

    public string SellerName { get; set; } = null!;

    public bool SellerNicVerified { get; set; }

    public int? RiskScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string FraudStatus { get; set; } = null!;

    public string? RiskSummary { get; set; }

    /// <summary>Total suspicious reports ever filed against this property.</summary>
    public int ReportCount { get; set; }

    /// <summary>Reports still Open or Under Review.</summary>
    public int OpenReportCount { get; set; }

    /// <summary>Days since UploadDate - lets the admin sort the oldest-waiting listings to the top.</summary>
    public int DaysWaiting { get; set; }
}
