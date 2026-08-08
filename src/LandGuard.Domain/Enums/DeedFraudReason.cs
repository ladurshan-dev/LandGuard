namespace LandGuard.Domain.Enums;

/// <summary>
/// One specific cause contributing to a <see cref="DeedVerificationStatus"/>
/// verdict (Phase 5A) - a <c>GovernmentDeedFraudDetectionResult.Reasons</c>
/// list can carry more than one of these at once (e.g. a NIC mismatch and a
/// Deed Number mismatch together also carry <see cref="MultipleFieldMismatch"/>).
/// Each value corresponds to exactly one of the material fields
/// <c>DeedFieldComparer.Compare</c> already produces a
/// <c>DeedFieldComparisonResult</c> for, plus the government-record-level
/// outcomes <c>GovernmentDeedComparisonService</c> already distinguishes
/// (not found / cancelled-or-otherwise-not-current / document unavailable).
/// No reason exists here that the existing Phase 4 comparison doesn't
/// already have a corresponding signal for - this enum only names outcomes
/// that were already possible, it does not add new ones.
/// </summary>
public enum DeedFraudReason
{
    /// <summary>DeedFieldComparisonResult "NIC" did not match.</summary>
    NicMismatch = 1,

    /// <summary>DeedFieldComparisonResult "OwnerName" did not match.</summary>
    OwnerNameMismatch = 2,

    /// <summary>DeedFieldComparisonResult "DeedNumber" did not match.</summary>
    DeedNumberMismatch = 3,

    /// <summary>DeedFieldComparisonResult "PropertyReference" did not match.</summary>
    PropertyReferenceMismatch = 4,

    /// <summary>DeedFieldComparisonResult "LandSize" differed beyond DeedFieldComparer's tolerance.</summary>
    LandSizeMismatch = 5,

    /// <summary>DeedFieldComparisonResult "District" did not match.</summary>
    DistrictMismatch = 6,

    /// <summary>DeedFieldComparisonResult "Address" did not match.</summary>
    AddressMismatch = 7,

    /// <summary>DeedFieldComparisonResult "RegistrationDate" did not match.</summary>
    RegistrationDateMismatch = 8,

    /// <summary>Two or more material fields mismatched at once - reported alongside the individual field reasons above, not instead of them, so no detail is lost.</summary>
    MultipleFieldMismatch = 9,

    /// <summary>DeedFieldComparisonResult "Price" exceeded DeedFieldComparer's anomaly threshold, and it was the only field that did not match. Never combined with any material-field reason above (see <c>GovernmentDeedFraudDetectionService</c> - a material mismatch always takes precedence over a price anomaly).</summary>
    PriceAnomalyDetected = 10,

    /// <summary>GovernmentDeedComparisonReport.GovernmentRecordFound was false - no trusted record could be resolved at all.</summary>
    GovernmentRecordNotFound = 11,

    /// <summary>A trusted record was found but its Status was not "Active" (Cancelled, or any other not-currently-valid status - see GovernmentLandRecordDto.Status's doc comment).</summary>
    GovernmentRecordCancelled = 12,

    /// <summary>A trusted, Active record was found, but its government deed document could not be opened/read for comparison (GovernmentDeedComparisonService's own "unavailable" outcome - see IFileStorageService.OpenDocumentAsync's doc comment).</summary>
    GovernmentDocumentUnavailable = 13
}
