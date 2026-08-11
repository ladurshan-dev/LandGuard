using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.DeedVerification</c> (Government Registry module, Phase
/// 5B) - one row per government deed verification run, the same "one row
/// per analysis run, append-only" shape <see cref="FraudCheck"/> already
/// establishes for the numeric fraud engine (see that entity's own doc
/// comment), applied here to <c>GovernmentDeedFraudDetectionService</c>'s
/// independent, evidence-based verdict instead - never written into
/// <c>FraudCheck</c> itself (the two systems stay deliberately separate).
///
/// Written exclusively by <c>usp_DeedVerification_Create</c>. There is
/// deliberately no update or delete stored procedure for this table (or
/// its two child tables, <see cref="DeedVerificationField"/> and
/// <see cref="DeedVerificationReason"/>) - a corrected re-verification
/// inserts a new row, it never edits an old one, for the same
/// audit-integrity reason <c>FraudCheck</c> is never updated either.
/// </summary>
public class DeedVerification
{
    public int DeedVerificationId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>
    /// The caller (Seller or Admin) who submitted the seller deed for this
    /// verification run - resolved server-side from
    /// <c>ICurrentUserService</c>, never a client-supplied value. This is
    /// only a record of who ran the check, never a claim that this user is
    /// fraudulent - see <c>DeedVerificationStatus.Fraudulent</c>'s own doc
    /// comment for the same distinction applied to the verdict itself.
    /// </summary>
    public int SubmittedByUserId { get; set; }

    /// <summary>
    /// <c>GovernmentLandRecordDto.RecordId</c>, e.g. "GR-000001" - a
    /// business-key string, deliberately with no foreign key: the
    /// government registry is not itself a database table in this project
    /// (see <c>DummyGovernmentRegistryService</c>, a fully in-memory
    /// stand-in for a future external government API). Null when no
    /// government record could be resolved at all.
    /// </summary>
    public string? GovernmentRecordId { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" | null, copied from <c>GovernmentLandRecordDto.Status</c> at the moment of this run.</summary>
    public string? GovernmentRecordStatus { get; set; }

    /// <summary>Stored as its exact string name (Verified/Fraudulent/PriceAnomaly/Unverified/UnverifiedCancelled/FormMismatch) - see DeedVerificationConfiguration for the enum conversion.</summary>
    public DeedVerificationStatus VerificationStatus { get; set; }

    public string? Summary { get; set; }

    /// <summary>
    /// The seller's uploaded-deed storage reference
    /// (<c>IFileStorageService</c>'s "documents/..." key). Always null as
    /// of Phase 5B: <c>GovernmentDeedComparisonReport</c> does not currently
    /// carry this value through from the seller's OCR result, and Phase 5B
    /// was explicitly scoped not to modify
    /// <c>GovernmentDeedComparisonService</c>/<c>GovernmentDeedComparisonReport</c>
    /// to add it - see <c>GovernmentDeedVerificationService</c>'s own doc
    /// comment. The column exists now so a future phase can populate it
    /// without a schema change.
    /// </summary>
    public string? SellerDocumentReference { get; set; }

    public DateTime VerifiedDate { get; set; }

    // Navigation properties -------------------------------------------------

    public Property Property { get; set; } = null!;

    public User SubmittedByUser { get; set; } = null!;

    public ICollection<DeedVerificationField> Fields { get; set; } = new List<DeedVerificationField>();

    public ICollection<DeedVerificationReason> Reasons { get; set; } = new List<DeedVerificationReason>();
}
