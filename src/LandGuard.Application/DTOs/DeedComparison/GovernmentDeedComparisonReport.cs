namespace LandGuard.Application.DTOs.DeedComparison;

/// <summary>
/// The result of comparing a seller's uploaded deed against the trusted
/// government record for a property (Government Registry module, Phase
/// 4) - returned directly to the caller by
/// <c>GovernmentDeedComparisonService</c>, never persisted.
///
/// Deliberately NOT merged into <c>dbo.FraudCheck</c>/<c>dbo.RiskReport</c>
/// or <c>DTOs.Fraud.FraudReportResponse</c>: <c>FraudCheck</c> has a fixed
/// 7-boolean-column shape written exclusively by
/// <c>usp_Fraud_AnalyseProperty</c>, which has no visibility into
/// <c>IGovernmentRegistryService</c>'s data (dummy today, a real external
/// API later) - there is no way to write this outcome into that table
/// without a schema change, which Phase 4 is explicitly scoped not to
/// make. Folding a government-verification outcome into the numeric
/// RiskScore is left for a later phase (adding one new, additive
/// <c>FraudRuleWeight</c> row and one new <c>FraudCheck</c> column, with
/// <c>usp_Fraud_AnalyseProperty</c> accepting the precomputed outcome as a
/// new parameter - the engine still evaluates every rule itself, this
/// service just becomes one more input to it, not a second scoring
/// system). Until then, this report stands alongside the existing fraud
/// report, not inside it.
/// </summary>
public class GovernmentDeedComparisonReport
{
    public int PropertyId { get; set; }

    /// <summary>The resolved GovernmentLandRecordDto.RecordId, or null if no government record could be resolved at all (Scenario F).</summary>
    public string? GovernmentRecordId { get; set; }

    public bool GovernmentRecordFound { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" | null (no record found at all).</summary>
    public string? GovernmentRecordStatus { get; set; }

    /// <summary>
    /// "Clean" | "Mismatch" | "MissingOrCancelledGovernmentRecord" |
    /// "FormMismatch". "FormMismatch" (Mandatory Deed / Form-vs-Deed
    /// Verification requirement) is produced BEFORE any Government
    /// Registry lookup is attempted, the moment
    /// <c>FormDeedComparer.Compare</c> finds the seller's own listing/
    /// account fields disagree with their own uploaded deed -
    /// <see cref="GovernmentRecordFound"/> stays false and
    /// <see cref="GovernmentRecordStatus"/> stays null for this outcome,
    /// since the government record is never even looked up.
    /// </summary>
    public string OverallOutcome { get; set; } = null!;

    /// <summary>
    /// Empty when OverallOutcome is "MissingOrCancelledGovernmentRecord" -
    /// there is nothing reliable to diff a field-by-field breakdown
    /// against. When OverallOutcome is "FormMismatch", this holds the
    /// FORM-vs-DEED comparison instead of a government comparison -
    /// <c>FormDeedComparer.Compare</c>'s output, each entry's
    /// <c>FieldName</c> prefixed "Form" (e.g. "FormOwnerNIC") precisely so
    /// it is never ambiguous with a government-comparison entry when read
    /// back later (see <c>FormDeedComparer</c>'s own doc comment for the
    /// GovernmentValue/SellerValue slot convention this reuse relies on).
    /// </summary>
    public IReadOnlyList<DeedFieldComparisonResult> Fields { get; set; } = Array.Empty<DeedFieldComparisonResult>();

    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// The seller's just-uploaded deed file's storage reference (Phase D -
    /// Seller Deed PDF Upload), i.e. the same value
    /// <c>OcrResultResponse.DocumentReference</c>/<c>StoredDocumentFile.StorageReference</c>
    /// already carry after <c>IOcrDocumentService.ExtractAsync</c> saves the
    /// file. Populated whenever OCR succeeds (both the normal comparison
    /// path and the "MissingOrCancelledGovernmentRecord" early-return path
    /// below, since the seller's document is already saved in either case)
    /// so <c>GovernmentDeedVerificationStoredProcedures.CreateVerificationAsync</c>
    /// can persist it as <c>DeedVerification.SellerDocumentReference</c>
    /// instead of always writing null. A storage key, never a raw
    /// filesystem path - see <c>StoredDocumentFile.StorageReference</c>'s own
    /// doc comment.
    /// </summary>
    public string? SellerDocumentReference { get; set; }

    /// <summary>
    /// Global Duplicate-Property Prevention requirement - the resolved
    /// GovernmentLandRecordDto.PropertyReference for this run. Set only
    /// when OverallOutcome is "Clean", "Mismatch" (price-only) or
    /// "DuplicateProperty" - i.e. only when a government record was
    /// actually resolved AND no MATERIAL field mismatched (see
    /// GovernmentDeedComparisonService.CompareAsync's own inline comment
    /// for exactly where this is decided); null for "FormMismatch",
    /// "MissingOrCancelledGovernmentRecord", or a material "Mismatch" -
    /// there is nothing trustworthy to persist for duplicate-detection
    /// purposes in those cases. Passed through unchanged to
    /// usp_Property_ApplyDeedVerificationOutcome by
    /// GovernmentDeedVerificationService.
    /// </summary>
    public string? GovernmentPropertyReference { get; set; }
}
