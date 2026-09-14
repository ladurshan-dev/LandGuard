namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// Authoritative deed-verification outcome breakdown for
/// <see cref="AdminFraudDashboardResponse"/> - counts only the LATEST
/// <c>dbo.DeedVerification</c> row per PropertyID (that table is an
/// append-only history; a re-verification inserts a new row rather than
/// updating the old one - see <c>DeedVerification</c>'s own doc comment).
/// <see cref="Total"/> is the number of properties with at least one
/// completed verification run, not the number of historical rows - see
/// <c>AdminDashboardService.GetDeedVerificationStatisticsAsync</c> for
/// exactly how "latest" is determined.
/// </summary>
public class AdminDashboardDeedVerificationStatistics
{
    public int Total { get; set; }

    public int Verified { get; set; }

    public int FormMismatch { get; set; }

    public int Fraudulent { get; set; }

    public int PriceAnomaly { get; set; }

    public int DuplicateProperty { get; set; }

    public int Unverified { get; set; }

    public int UnverifiedCancelled { get; set; }
}
