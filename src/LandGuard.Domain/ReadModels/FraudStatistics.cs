namespace LandGuard.Domain.ReadModels;

/// <summary>
/// Keyless, single-row read model mapped to <c>dbo.vw_FraudStatistics</c> -
/// the admin dashboard summary. Always returns exactly one row (every
/// column is an independent scalar subquery). Backs
/// <c>usp_Admin_GetDashboard</c>'s first result set.
/// </summary>
public class FraudStatistics
{
    public int TotalBuyers { get; set; }

    public int TotalSellers { get; set; }

    public int VerifiedSellers { get; set; }

    public int SuspendedUsers { get; set; }

    public int TotalProperties { get; set; }

    public int ApprovedProperties { get; set; }

    public int PendingProperties { get; set; }

    public int FlaggedProperties { get; set; }

    public int RejectedProperties { get; set; }

    public int LowRiskCount { get; set; }

    public int MediumRiskCount { get; set; }

    public int HighRiskCount { get; set; }

    /// <summary>Null if no listing has been analysed yet.</summary>
    public decimal? AverageRiskScore { get; set; }

    public int OpenSuspiciousReports { get; set; }

    public int TotalPodcasts { get; set; }
}
