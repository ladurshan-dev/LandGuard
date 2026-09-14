namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// GET /api/admin/dashboard's whole response body - the Admin Fraud
/// Dashboard. Six independently-sourced sections, each read-only and each
/// reusing infrastructure that already exists (see
/// <c>IAdminDashboardService</c>'s own doc comment for exactly what backs
/// each one). Nothing here is written back to the database; this DTO only
/// ever flows out of <c>AdminDashboardService.GetDashboardAsync</c>.
/// </summary>
public class AdminFraudDashboardResponse
{
    public AdminDashboardUserStatistics Users { get; set; } = null!;

    public AdminDashboardPropertyStatistics Properties { get; set; } = null!;

    public AdminDashboardDeedVerificationStatistics DeedVerification { get; set; } = null!;

    public AdminDashboardRiskStatistics Risk { get; set; } = null!;

    public IReadOnlyList<AdminDashboardRuleTriggerItem> RuleTriggers { get; set; } = Array.Empty<AdminDashboardRuleTriggerItem>();

    /// <summary>Top 10 at most - see <c>AdminDashboardService.GetReviewPropertiesAsync</c>.</summary>
    public IReadOnlyList<AdminDashboardReviewPropertySummary> ReviewProperties { get; set; } = Array.Empty<AdminDashboardReviewPropertySummary>();
}
