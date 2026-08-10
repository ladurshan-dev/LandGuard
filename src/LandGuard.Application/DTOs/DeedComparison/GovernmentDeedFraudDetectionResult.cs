using LandGuard.Domain.Enums;

namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// The result of classifying a <see cref="GovernmentDeedComparisonReport"/>
/// (Government Registry module, Phase 4) into a
/// <see cref="DeedVerificationStatus"/> verdict with reasons - Phase 5A.
/// Produced by <c>GovernmentDeedFraudDetectionService.Classify</c>, purely
/// from an already-computed report; like
/// <see cref="GovernmentDeedComparisonReport"/> itself, not persisted
/// anywhere yet (persistence is explicitly out of scope for Phase 5A - see
/// that service's doc comment).
///
/// <see cref="Evidence"/> is the exact <see cref="DeedFieldComparisonResult"/>
/// list <see cref="GovernmentDeedComparisonReport.Fields"/> already
/// produced - passed through unchanged, not recomputed or duplicated into a
/// new shape, per Phase 5A's explicit "do not duplicate the field
/// comparison logic" instruction.
/// </summary>
public class GovernmentDeedFraudDetectionResult
{
    /// <summary>Copied from <see cref="GovernmentDeedComparisonReport.PropertyId"/> so this result is self-contained without also requiring the original report.</summary>
    public int PropertyId { get; set; }

    /// <summary>Copied from <see cref="GovernmentDeedComparisonReport.GovernmentRecordId"/> - null only when no government record could be resolved at all.</summary>
    public string? GovernmentRecordId { get; set; }

    /// <summary>Copied from <see cref="GovernmentDeedComparisonReport.GovernmentRecordStatus"/> - "Active" | "Cancelled" | "Suspended" | null.</summary>
    public string? GovernmentRecordStatus { get; set; }

    public DeedVerificationStatus Status { get; set; }

    /// <summary>
    /// Every reason contributing to <see cref="Status"/>, empty only when
    /// <see cref="Status"/> is <see cref="DeedVerificationStatus.Verified"/>.
    /// Machine-readable codes - see <see cref="DeedFraudReason"/> - intended
    /// for a future API/UI consumer to key off (the same role
    /// <c>FraudRuleResponse.RuleCode</c> already plays for the numeric
    /// engine), with <see cref="Summary"/> carrying the human-readable form.
    /// </summary>
    public IReadOnlyList<DeedFraudReason> Reasons { get; set; } = Array.Empty<DeedFraudReason>();

    /// <summary>
    /// Human-readable explanation of <see cref="Status"/>/<see cref="Reasons"/>,
    /// shown to a Seller/Admin/Buyer (audience-appropriate filtering is a
    /// later phase's concern, not this one) - the same role
    /// <c>RiskReport.Summary</c> already plays for the numeric fraud engine.
    /// Built from fixed, reviewed wording per reason (see
    /// <c>GovernmentDeedFraudDetectionService.DescribeReason</c>), reusing
    /// <see cref="DeedFieldComparisonResult.Message"/> for field-level detail
    /// in <see cref="Evidence"/> rather than re-deriving it here.
    /// </summary>
    public string Summary { get; set; } = null!;

    /// <summary>
    /// The exact field-by-field comparison <see cref="GovernmentDeedComparisonReport.Fields"/>
    /// already computed - <see cref="DeedFieldComparisonResult.FieldName"/>/
    /// <see cref="DeedFieldComparisonResult.GovernmentValue"/>/
    /// <see cref="DeedFieldComparisonResult.SellerValue"/>/
    /// <see cref="DeedFieldComparisonResult.Match"/>/
    /// <see cref="DeedFieldComparisonResult.Message"/> - passed through
    /// unchanged, never recomputed. Empty when <see cref="Status"/> is
    /// <see cref="DeedVerificationStatus.Unverified"/> or
    /// <see cref="DeedVerificationStatus.UnverifiedCancelled"/>, exactly
    /// mirroring when <see cref="GovernmentDeedComparisonReport.Fields"/>
    /// itself is empty.
    /// </summary>
    public IReadOnlyList<DeedFieldComparisonResult> Evidence { get; set; } = Array.Empty<DeedFieldComparisonResult>();

    /// <summary>Copied from <see cref="GovernmentDeedComparisonReport.GeneratedDate"/> - when the underlying comparison was produced, not when it was classified (classification is a pure, synchronous, immediately-following step - see <c>GovernmentDeedFraudDetectionService</c>).</summary>
    public DateTime GeneratedDate { get; set; }

    /// <summary>Copied from <see cref="GovernmentDeedComparisonReport.SellerDocumentReference"/> (Phase D) - passed through unchanged by <c>GovernmentDeedFraudDetectionService.BuildResult</c> so <c>GovernmentDeedVerificationStoredProcedures.CreateVerificationAsync</c> can persist it, regardless of which <see cref="DeedVerificationStatus"/> was reached.</summary>
    public string? SellerDocumentReference { get; set; }
}
