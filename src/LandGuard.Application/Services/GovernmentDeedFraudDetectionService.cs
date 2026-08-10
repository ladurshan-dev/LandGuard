using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.DeedComparison;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Classifies a <see cref="GovernmentDeedComparisonReport"/> (Government
/// Registry module, Phase 4 - produced by
/// <c>GovernmentDeedComparisonService.CompareAsync</c>) into a
/// <see cref="GovernmentDeedFraudDetectionResult"/> verdict - Phase 5A.
///
/// <b>Pure interpretation only.</b> This class reads the report's own
/// <c>OverallOutcome</c>/<c>GovernmentRecordFound</c>/<c>GovernmentRecordStatus</c>/
/// <c>Fields</c> and produces a verdict from them - it makes no HTTP call, no
/// SQL/Dapper call, no filesystem access, no OCR call, and does not call
/// <c>FraudDetectionService</c>/<c>IFraudDetectionService</c> or touch
/// <c>dbo.FraudCheck</c>/<c>dbo.FraudRuleWeight</c>/<c>dbo.RiskReport</c> in
/// any way. It does not re-run <c>DeedFieldComparer</c> or duplicate its
/// field-comparison logic - every fact this class needs
/// (<c>DeedFieldComparisonResult.FieldName</c>/<c>Match</c>) already exists
/// on the report it is handed.
///
/// <b>Why this stays independent of the numeric fraud engine.</b>
/// <c>dbo.FraudCheck</c> is a fixed 7-boolean-column shape written
/// exclusively by <c>usp_Fraud_AnalyseProperty</c> (T-SQL), which has no
/// visibility into <see cref="IGovernmentRegistryService"/>'s data - there
/// is no column here to write this outcome into without a schema change,
/// and Phase 5A is explicitly scoped not to make one (see
/// <c>GovernmentDeedComparisonReport</c>'s own doc comment, which floats
/// that possibility for "a later phase" - Phase 5A is deliberately not that
/// phase; the governing instruction for this phase is that government deed
/// verification and the numeric engine remain two independent systems, full
/// stop, not merely deferred). <see cref="DeedVerificationStatus"/> is
/// therefore its own enum, not a reuse of <c>FraudStatus</c> - see that
/// enum's doc comment.
///
/// <b>What "Fraudulent" means here.</b>
/// <see cref="DeedVerificationStatus.Fraudulent"/> is a statement about the
/// uploaded DOCUMENT failing verification against the trusted government
/// record - never a statement about the seller/account. This class has no
/// concept of a <c>User</c> or <c>Property.SellerId</c> at all (it never
/// sees either - only the comparison report), so it is structurally
/// incapable of marking an account as fraudulent even by accident. Whether
/// a pattern of fraudulent-document verdicts should ever inform a decision
/// about the seller's account is an Admin's own, separate judgement call,
/// not something this classification makes automatically.
///
/// <b>MATERIAL fields.</b> NIC, OwnerName, DeedNumber, PropertyReference,
/// LandSize, District, Address, RegistrationDate - i.e. every
/// <c>DeedFieldComparisonResult.FieldName</c> <c>DeedFieldComparer.Compare</c>
/// produces except "Price". A mismatch in any one of these is sufficient
/// for <see cref="DeedVerificationStatus.Fraudulent"/>; see
/// <see cref="MaterialFieldReasons"/> and <see cref="Classify"/>.
///
/// <b>Price is not material.</b> <c>DeedFieldComparer.ComparePrice</c>
/// already treats the seller's current asking price and the government's
/// historical registered price as two different business concepts that are
/// expected to differ even for a completely genuine listing (see that
/// method's own doc comment) - this class reuses that existing "Price"
/// result exactly as computed (never re-deriving the 50% threshold or any
/// other price logic of its own) and only ever produces
/// <see cref="DeedVerificationStatus.PriceAnomaly"/> from it, and only when
/// it is the sole mismatch.
/// </summary>
public class GovernmentDeedFraudDetectionService : IGovernmentDeedFraudDetectionService
{
    /// <summary>
    /// MATERIAL <c>DeedFieldComparisonResult.FieldName</c> values, each
    /// mapped to the specific <see cref="DeedFraudReason"/> it produces when
    /// mismatched. "Price" is deliberately absent - see this class's own
    /// doc comment.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, DeedFraudReason> MaterialFieldReasons =
        new Dictionary<string, DeedFraudReason>(StringComparer.Ordinal)
        {
            ["NIC"] = DeedFraudReason.NicMismatch,
            ["OwnerName"] = DeedFraudReason.OwnerNameMismatch,
            ["DeedNumber"] = DeedFraudReason.DeedNumberMismatch,
            ["PropertyReference"] = DeedFraudReason.PropertyReferenceMismatch,
            ["LandSize"] = DeedFraudReason.LandSizeMismatch,
            ["District"] = DeedFraudReason.DistrictMismatch,
            ["Address"] = DeedFraudReason.AddressMismatch,
            ["RegistrationDate"] = DeedFraudReason.RegistrationDateMismatch
        };

    private const string PriceFieldName = "Price";

    public GovernmentDeedFraudDetectionResult Classify(GovernmentDeedComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return report.OverallOutcome switch
        {
            "MissingOrCancelledGovernmentRecord" => ClassifyMissingOrCancelled(report),
            "Clean" => BuildResult(report, DeedVerificationStatus.Verified, Array.Empty<DeedFraudReason>()),
            "Mismatch" => ClassifyMismatch(report),

            // GovernmentDeedComparisonService only ever produces one of the
            // three outcomes above. An unrecognised value would mean a
            // future change to that service introduced a new outcome this
            // classifier was never updated for - surfacing that loudly as a
            // defensive exception is safer than silently guessing a status,
            // and per this phase's own instruction, GovernmentDeedComparisonService
            // is not something this change is allowed to modify to add a
            // new outcome without flagging it first.
            _ => throw new InvalidOperationException(
                $"Unrecognised GovernmentDeedComparisonReport.OverallOutcome '{report.OverallOutcome}' - GovernmentDeedFraudDetectionService has no classification rule for it.")
        };
    }

    /// <summary>
    /// The report's own three sub-cases of "MissingOrCancelledGovernmentRecord"
    /// (see <c>GovernmentDeedComparisonService.CompareAsync</c>) are
    /// distinguishable from exactly the two fields this report already
    /// exposes: <see cref="GovernmentDeedComparisonReport.GovernmentRecordFound"/>
    /// separates "no record at all" from "a record was found," and
    /// <see cref="GovernmentDeedComparisonReport.GovernmentRecordStatus"/>
    /// separates "found but not Active" from "found, Active, but its PDF
    /// could not be opened" - if a record was found and its status reads
    /// "Active", this outcome can only have been reached because the PDF
    /// was unavailable (GovernmentDeedComparisonService's own logic never
    /// reaches "MissingOrCancelledGovernmentRecord" for a found, Active
    /// record any other way).
    /// </summary>
    private static GovernmentDeedFraudDetectionResult ClassifyMissingOrCancelled(GovernmentDeedComparisonReport report)
    {
        if (!report.GovernmentRecordFound)
        {
            return BuildResult(report, DeedVerificationStatus.Unverified, new[] { DeedFraudReason.GovernmentRecordNotFound });
        }

        if (string.Equals(report.GovernmentRecordStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResult(report, DeedVerificationStatus.Unverified, new[] { DeedFraudReason.GovernmentDocumentUnavailable });
        }

        // Not Active - "Cancelled" per the seed data, or any other
        // not-currently-valid status GovernmentLandRecordDto.Status's own
        // doc comment documents as possible ("Suspended"). That doc comment
        // already groups Cancelled/Suspended together as "the government no
        // longer recognises as currently valid," so both are reported with
        // the same GovernmentRecordCancelled reason here rather than
        // inventing a second, unrequested reason for a status value no
        // seed record actually uses.
        return BuildResult(report, DeedVerificationStatus.UnverifiedCancelled, new[] { DeedFraudReason.GovernmentRecordCancelled });
    }

    private static GovernmentDeedFraudDetectionResult ClassifyMismatch(GovernmentDeedComparisonReport report)
    {
        var materialMismatches = report.Fields
            .Where(f => !f.Match && MaterialFieldReasons.ContainsKey(f.FieldName))
            .ToList();

        if (materialMismatches.Count > 0)
        {
            var reasons = new List<DeedFraudReason>();

            if (materialMismatches.Count > 1)
            {
                // Reported alongside the individual field reasons, not
                // instead of them, so no per-field detail is lost from the
                // reason list (the field-level detail remains fully
                // available either way via GovernmentDeedFraudDetectionResult.Evidence).
                reasons.Add(DeedFraudReason.MultipleFieldMismatch);
            }

            reasons.AddRange(materialMismatches.Select(f => MaterialFieldReasons[f.FieldName]));

            return BuildResult(report, DeedVerificationStatus.Fraudulent, reasons);
        }

        var priceMismatch = report.Fields.FirstOrDefault(f => f.FieldName == PriceFieldName && !f.Match);
        if (priceMismatch is not null)
        {
            return BuildResult(report, DeedVerificationStatus.PriceAnomaly, new[] { DeedFraudReason.PriceAnomalyDetected });
        }

        // Defensive fallback only: DeedFieldComparer.Compare's own
        // "OverallOutcome = Clean iff every field Match" rule means a
        // "Mismatch" report is only ever produced when at least one field
        // above did not match, so this is unreachable for any report
        // GovernmentDeedComparisonService actually produces today. Kept
        // rather than assumed away so a future, unrelated change to that
        // invariant fails safely (Verified, no reasons) instead of
        // returning enum default(0).
        return BuildResult(report, DeedVerificationStatus.Verified, Array.Empty<DeedFraudReason>());
    }

    private static GovernmentDeedFraudDetectionResult BuildResult(
        GovernmentDeedComparisonReport report, DeedVerificationStatus status, IReadOnlyList<DeedFraudReason> reasons) => new()
    {
        PropertyId = report.PropertyId,
        GovernmentRecordId = report.GovernmentRecordId,
        GovernmentRecordStatus = report.GovernmentRecordStatus,
        Status = status,
        Reasons = reasons,
        Summary = BuildSummary(status, reasons),
        // Evidence is Unverified/UnverifiedCancelled-empty exactly when
        // report.Fields itself is empty (see GovernmentDeedComparisonReport.Fields'
        // own doc comment) - passed through unchanged, never recomputed.
        Evidence = report.Fields,
        GeneratedDate = report.GeneratedDate,
        // Passed through unchanged regardless of status - this is the one
        // shared factory every classification branch already funnels
        // through, so this line does not touch any classification rule
        // above (Classify/ClassifyMissingOrCancelled/ClassifyMismatch/
        // MaterialFieldReasons are all unmodified).
        SellerDocumentReference = report.SellerDocumentReference
    };

    /// <summary>
    /// Fixed, reviewed wording per reason - not a generic template, so each
    /// sentence reads naturally regardless of which reason(s) apply. Reuses
    /// no text from <c>DeedFieldComparisonResult.Message</c> here
    /// (that per-field text remains available, unchanged, via
    /// <see cref="GovernmentDeedFraudDetectionResult.Evidence"/>) - this
    /// summary is a different, higher-level statement about the DOCUMENT's
    /// verification outcome as a whole, the same distinction
    /// <c>RiskReport.Summary</c> already draws from the per-rule
    /// <c>FraudRuleResponse.Message</c> list it stands alongside.
    /// </summary>
    private static string BuildSummary(DeedVerificationStatus status, IReadOnlyList<DeedFraudReason> reasons)
    {
        if (status == DeedVerificationStatus.Verified)
        {
            return "The uploaded deed matches the trusted government registry record.";
        }

        var sentences = reasons.Select(DescribeReason).Distinct();

        return string.Join(" ", sentences);
    }

    private static string DescribeReason(DeedFraudReason reason) => reason switch
    {
        DeedFraudReason.NicMismatch =>
            "The NIC on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.OwnerNameMismatch =>
            "The owner name on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.DeedNumberMismatch =>
            "The deed number on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.PropertyReferenceMismatch =>
            "The property reference on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.LandSizeMismatch =>
            "The land size on the uploaded deed differs from the trusted government registry beyond the permitted tolerance.",
        DeedFraudReason.DistrictMismatch =>
            "The district on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.AddressMismatch =>
            "The address on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.RegistrationDateMismatch =>
            "The registration date on the uploaded deed does not match the trusted government registry record.",
        DeedFraudReason.MultipleFieldMismatch =>
            "The uploaded deed contains multiple material differences from the trusted government registry record.",
        DeedFraudReason.PriceAnomalyDetected =>
            "The current asking price differs significantly from the historical government registered price. This is a price anomaly and is not by itself evidence that the deed is fraudulent.",
        DeedFraudReason.GovernmentRecordNotFound =>
            "No matching government registry record could be found, so the uploaded deed cannot be verified.",
        DeedFraudReason.GovernmentRecordCancelled =>
            "The government registry record is cancelled, so the uploaded deed cannot be verified against an active government record.",
        DeedFraudReason.GovernmentDocumentUnavailable =>
            "The government registry record is active, but its trusted deed document could not be retrieved, so the uploaded deed cannot be verified.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unrecognised DeedFraudReason.")
    };
}
