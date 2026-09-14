namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// One row of <see cref="AdminFraudDashboardResponse.ReviewProperties"/> -
/// a deliberately MINIMAL projection of
/// <c>LandGuard.Domain.ReadModels.FlaggedProperty</c>
/// (<c>dbo.vw_FlaggedProperty</c>), never the full read model itself.
///
/// PRIVACY: this type intentionally has no Seller NIC, Owner NIC, Deed
/// Reference, OCR text, Government Registry values, seller phone/email, or
/// verification field evidence - none of those are read from
/// <c>FlaggedProperty</c> when building this summary (see
/// <c>AdminDashboardService.GetReviewPropertiesAsync</c>'s projection),
/// even though the source view itself carries several of them (e.g.
/// <c>DeedReference</c>, <c>SellerName</c>). Only the seven fields below
/// are ever selected onto the wire.
/// </summary>
public class AdminDashboardReviewPropertySummary
{
    public int PropertyId { get; set; }

    public string Title { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>Supporting risk-engine indicator only - null until at least one fraud analysis run exists.</summary>
    public int? RiskScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    /// <summary>Days since the listing's UploadDate.</summary>
    public int DaysWaiting { get; set; }

    /// <summary>Suspicious reports still Open or Under Review.</summary>
    public int OpenReportCount { get; set; }
}
