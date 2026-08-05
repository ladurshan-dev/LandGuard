namespace LandGuard.Domain.Enums;

/// <summary>
/// Workflow state of a <c>dbo.SuspiciousReport</c> row (<c>CK_SuspiciousReport_Status</c>,
/// FR12). The database stores the middle value as the two-word string
/// "Under Review" (spaces aren't valid in a C# identifier), so
/// <see cref="UnderReview"/> is mapped to that exact string by a custom
/// EF Core value converter in <c>SuspiciousReportConfiguration</c> rather
/// than the default enum-to-string conversion.
/// </summary>
public enum ReportStatus
{
    Open = 1,
    UnderReview = 2,
    Resolved = 3
}
