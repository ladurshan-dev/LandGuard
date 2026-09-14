namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// User-account section of <see cref="AdminFraudDashboardResponse"/>.
/// Sourced entirely from <c>dbo.vw_FraudStatistics</c> (via
/// <c>IApplicationDbContext.FraudStatistics</c>) - no new query, this view
/// already computes exactly these four figures.
/// </summary>
public class AdminDashboardUserStatistics
{
    public int TotalBuyers { get; set; }

    public int TotalSellers { get; set; }

    public int VerifiedSellers { get; set; }

    public int SuspendedUsers { get; set; }
}
