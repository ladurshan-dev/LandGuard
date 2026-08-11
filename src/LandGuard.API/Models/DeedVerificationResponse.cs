using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;
using LandGuard.Domain.Enums;

namespace LandGuard.API.Models;

/// <summary>
/// POST /api/deed-verification/{propertyId}'s response - Government
/// Registry module, Phase 5C. A thin, human-readable reshaping of
/// <see cref="GovernmentDeedVerificationOutcome"/> (Phase 5B, unmodified)
/// for the API boundary only: flattens <c>FraudDetectionResult</c>'s
/// fields to the top level and attaches a short, fixed description to each
/// <see cref="DeedFraudReason"/> code for the <see cref="Reasons"/> array.
///
/// <see cref="Evidence"/> reuses <see cref="DeedFieldComparisonResult"/>
/// exactly as produced by Phase 4/5A - not reshaped or duplicated, per this
/// phase's "use the existing DTOs wherever possible" instruction.
///
/// <see cref="DescribeReason"/> is a new, separate mapping - not a call
/// into <c>GovernmentDeedFraudDetectionService.DescribeReason</c>, which is
/// private and lives in a file this phase must not modify. This is
/// presentation text only (one short sentence per reason code for the API
/// response); it introduces no new classification rule, reuses no
/// judgement <c>GovernmentDeedFraudDetectionService.Classify</c> hasn't
/// already made, and <see cref="Summary"/> (the authoritative,
/// already-composed explanation) is passed through unchanged alongside it.
/// </summary>
public class DeedVerificationResponse
{
    /// <summary>The newly-created <c>DeedVerification</c> row's id (Phase 5B) - lets a caller reference this exact run later, e.g. via a future history endpoint.</summary>
    public int DeedVerificationId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>"Verified" | "Fraudulent" | "PriceAnomaly" | "Unverified" | "UnverifiedCancelled" - DeedVerificationStatus's exact string name.</summary>
    public string VerificationStatus { get; set; } = null!;

    public string? GovernmentRecordId { get; set; }

    /// <summary>"Active" | "Cancelled" | "Suspended" | null.</summary>
    public string? GovernmentRecordStatus { get; set; }

    /// <summary>The authoritative, already-composed explanation from GovernmentDeedFraudDetectionService.Classify - never re-derived here.</summary>
    public string Summary { get; set; } = null!;

    public IReadOnlyList<DeedVerificationReasonEntry> Reasons { get; set; } = Array.Empty<DeedVerificationReasonEntry>();

    public IReadOnlyList<DeedFieldComparisonResult> Evidence { get; set; } = Array.Empty<DeedFieldComparisonResult>();

    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// The seller's uploaded deed document's storage reference (Phase D),
    /// copied from <see cref="GovernmentDeedFraudDetectionResult.SellerDocumentReference"/>
    /// - a storage key an authenticated retrieval endpoint could resolve
    /// later (see <c>StoredDocumentFile.StorageReference</c>'s own doc
    /// comment), never a raw filesystem path. No document-download endpoint
    /// exists in this phase, so the frontend uses this only to confirm a
    /// deed was uploaded and verified, not to fetch/display the file
    /// itself.
    /// </summary>
    public string? SellerDocumentReference { get; set; }

    public static DeedVerificationResponse FromOutcome(GovernmentDeedVerificationOutcome outcome) =>
        FromFraudDetectionResult(outcome.DeedVerificationId, outcome.FraudDetectionResult);

    /// <summary>
    /// Builds the identical response shape from a persisted
    /// <see cref="DeedVerificationHistoryEntry"/> (Phase D's new GET
    /// verification-read endpoint) - reused by
    /// <see cref="FromOutcome"/> above so the POST (just-ran verification)
    /// and GET (previously-persisted verification) responses are
    /// field-for-field identical, and so this class's existing
    /// <see cref="DescribeReason"/> mapping serves both without
    /// duplication.
    /// </summary>
    public static DeedVerificationResponse FromHistoryEntry(DeedVerificationHistoryEntry entry)
    {
        var record = entry.Record;

        return new DeedVerificationResponse
        {
            DeedVerificationId = record.DeedVerificationId,
            PropertyId = record.PropertyId,
            VerificationStatus = record.VerificationStatus,
            GovernmentRecordId = record.GovernmentRecordId,
            GovernmentRecordStatus = record.GovernmentRecordStatus,
            Summary = record.Summary ?? string.Empty,
            Reasons = entry.Reasons
                .Select(reason => new DeedVerificationReasonEntry
                {
                    Reason = reason.Reason,
                    Description = Enum.TryParse<DeedFraudReason>(reason.Reason, out var parsed) ? DescribeReason(parsed) : reason.Reason
                })
                .ToList(),
            Evidence = entry.Fields
                .Select(field => new DeedFieldComparisonResult
                {
                    FieldName = field.FieldName,
                    GovernmentValue = field.GovernmentValue,
                    SellerValue = field.SellerValue,
                    Match = field.IsMatch,
                    Message = field.Message ?? string.Empty
                })
                .ToList(),
            GeneratedDate = record.VerifiedDate,
            SellerDocumentReference = record.SellerDocumentReference
        };
    }

    private static DeedVerificationResponse FromFraudDetectionResult(int deedVerificationId, GovernmentDeedFraudDetectionResult result) =>
        new()
        {
            DeedVerificationId = deedVerificationId,
            PropertyId = result.PropertyId,
            VerificationStatus = result.Status.ToString(),
            GovernmentRecordId = result.GovernmentRecordId,
            GovernmentRecordStatus = result.GovernmentRecordStatus,
            Summary = result.Summary,
            Reasons = result.Reasons
                .Select(reason => new DeedVerificationReasonEntry { Reason = reason.ToString(), Description = DescribeReason(reason) })
                .ToList(),
            Evidence = result.Evidence,
            GeneratedDate = result.GeneratedDate,
            SellerDocumentReference = result.SellerDocumentReference
        };

    /// <summary>
    /// Fixed, reviewed one-sentence text per <see cref="DeedFraudReason"/> -
    /// deliberately shorter/plainer than
    /// <c>GovernmentDeedFraudDetectionService.DescribeReason</c>'s own
    /// wording (which already appears, unchanged, inside
    /// <see cref="Summary"/>): this exists only so each entry in
    /// <see cref="Reasons"/> is independently readable without parsing the
    /// combined summary sentence.
    /// </summary>
    private static string DescribeReason(DeedFraudReason reason) => reason switch
    {
        DeedFraudReason.NicMismatch => "The NIC on the uploaded deed differs from the government registry.",
        DeedFraudReason.OwnerNameMismatch => "The owner name on the uploaded deed differs from the government registry.",
        DeedFraudReason.DeedNumberMismatch => "The deed number on the uploaded deed differs from the government registry.",
        DeedFraudReason.PropertyReferenceMismatch => "The property reference on the uploaded deed differs from the government registry.",
        DeedFraudReason.LandSizeMismatch => "The land size on the uploaded deed differs from the government registry beyond the permitted 1-perch tolerance.",
        DeedFraudReason.DistrictMismatch => "The district on the uploaded deed differs from the government registry.",
        DeedFraudReason.AddressMismatch => "The address on the uploaded deed differs from the government registry.",
        DeedFraudReason.RegistrationDateMismatch => "The registration date on the uploaded deed differs from the government registry.",
        DeedFraudReason.MultipleFieldMismatch => "The uploaded deed differs from the government registry in more than one material field.",
        DeedFraudReason.PriceAnomalyDetected => "The asking price differs significantly from the government's registered price. This alone is not evidence that the deed is fraudulent.",
        DeedFraudReason.GovernmentRecordNotFound => "No matching government registry record could be found for this property.",
        DeedFraudReason.GovernmentRecordCancelled => "The government registry record is cancelled, so the deed could not be verified against an active record. This does not by itself mean the deed is fraudulent.",
        DeedFraudReason.GovernmentDocumentUnavailable => "The government registry record is active, but its trusted document could not be retrieved for comparison.",
        DeedFraudReason.FormSellerNicMismatch => "Seller NIC does not match the uploaded deed.",
        DeedFraudReason.FormOwnerNameMismatch => "Owner name does not match the uploaded deed.",
        DeedFraudReason.FormDeedNumberMismatch => "Deed number does not match the uploaded deed.",
        DeedFraudReason.FormLocationMismatch => "Location does not match the uploaded deed.",
        DeedFraudReason.FormDistrictMismatch => "District does not match the uploaded deed.",
        DeedFraudReason.FormLandSizeMismatch => "Land extent does not match the uploaded deed.",
        DeedFraudReason.FormOwnerNicMismatch => "Owner NIC does not match the uploaded deed.",
        DeedFraudReason.FormOwnerAddressMismatch => "Owner address does not match the uploaded deed.",
        _ => reason.ToString()
    };
}

/// <summary>One entry of <see cref="DeedVerificationResponse.Reasons"/> - a machine-readable code plus a short human-readable sentence.</summary>
public class DeedVerificationReasonEntry
{
    /// <summary>DeedFraudReason's exact string name, e.g. "NicMismatch".</summary>
    public string Reason { get; set; } = null!;

    public string Description { get; set; } = null!;
}
