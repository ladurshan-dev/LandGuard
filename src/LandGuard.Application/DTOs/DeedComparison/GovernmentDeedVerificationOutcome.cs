namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// The result of <c>GovernmentDeedVerificationService.VerifyAndPersistAsync</c>
/// (Phase 5B) - Phase 5A's <see cref="GovernmentDeedFraudDetectionResult"/>
/// plus the <c>DeedVerificationID</c> its persistence just created, so a
/// future Phase 5C caller can link back to the stored row (e.g. to fetch it
/// again later via <c>usp_DeedVerification_GetHistory</c>) without a second
/// lookup.
/// </summary>
public class GovernmentDeedVerificationOutcome
{
    public int DeedVerificationId { get; set; }

    public GovernmentDeedFraudDetectionResult FraudDetectionResult { get; set; } = null!;
}
