namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// Current <c>Property.Status</c> breakdown for
/// <see cref="AdminFraudDashboardResponse"/> - deliberately computed live
/// from <c>IApplicationDbContext.Properties</c> rather than
/// <c>dbo.vw_FraudStatistics</c> (that view's own Approved/Pending/
/// Flagged/Rejected counts predate <c>PropertyStatus.Withdrawn</c> and
/// <c>PropertyStatus.Disapproved</c>, so it cannot report either without
/// itself being changed - see <c>AdminDashboardService.GetPropertyStatisticsAsync</c>).
/// </summary>
public class AdminDashboardPropertyStatistics
{
    public int Total { get; set; }

    public int Approved { get; set; }

    public int Pending { get; set; }

    public int Flagged { get; set; }

    public int Rejected { get; set; }

    public int Withdrawn { get; set; }

    public int Disapproved { get; set; }
}
