namespace LandGuard.Application.Common.Models;

/// <summary>
/// One row of <c>dbo.DeedVerification</c>, as returned by
/// <c>usp_DeedVerification_GetHistory</c>'s first result set - a dedicated
/// Dapper-projection DTO, not the Domain <c>DeedVerification</c> entity,
/// following the same reasoning <c>FraudHistoryEntry</c>/
/// <c>PropertyListingResult</c> already established.
/// <see cref="VerificationStatus"/>/<see cref="GovernmentRecordStatus"/> are
/// raw strings here (not the <c>DeedVerificationStatus</c> enum) since this
/// is exactly what the database returns - mapping back to the enum, if ever
/// needed, is the caller's job, not this DTO's.
/// </summary>
public class DeedVerificationRecord
{
    public int DeedVerificationId { get; set; }

    public int PropertyId { get; set; }

    public int SubmittedByUserId { get; set; }

    public string? GovernmentRecordId { get; set; }

    public string? GovernmentRecordStatus { get; set; }

    /// <summary>"Verified" | "Fraudulent" | "PriceAnomaly" | "Unverified" | "UnverifiedCancelled" | "FormMismatch" - DeedVerificationStatus's exact string name.</summary>
    public string VerificationStatus { get; set; } = null!;

    public string? Summary { get; set; }

    public string? SellerDocumentReference { get; set; }

    public DateTime VerifiedDate { get; set; }
}
