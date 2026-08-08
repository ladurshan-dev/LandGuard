using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.DeedVerificationReason</c> (Government Registry module,
/// Phase 5B) - one row per <see cref="DeedFraudReason"/> contributing to a
/// <see cref="DeedVerification"/>'s <c>VerificationStatus</c> -
/// <c>GovernmentDeedFraudDetectionResult.Reasons</c> can carry more than
/// one at once (e.g. a NIC mismatch and a Deed Number mismatch together
/// also carry <see cref="DeedFraudReason.MultipleFieldMismatch"/>), hence a
/// separate child table rather than a single column.
///
/// Written exclusively by <c>usp_DeedVerificationReason_Add</c>, one call
/// per reason, inside the same transaction as the parent
/// <c>usp_DeedVerification_Create</c> insert. No update/delete procedure -
/// see <see cref="DeedVerification"/>'s own doc comment.
/// </summary>
public class DeedVerificationReason
{
    public int DeedVerificationReasonId { get; set; }

    public int DeedVerificationId { get; set; }

    /// <summary>Stored as its exact string name (e.g. "NicMismatch", "GovernmentRecordCancelled") - see DeedVerificationReasonConfiguration for the enum conversion.</summary>
    public DeedFraudReason Reason { get; set; }

    // Navigation properties -------------------------------------------------

    public DeedVerification DeedVerification { get; set; } = null!;
}
