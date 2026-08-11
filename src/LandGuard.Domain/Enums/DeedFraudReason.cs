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
    GovernmentDocumentUnavailable = 13,

    // ---- Added for the Mandatory Deed / Form-vs-Deed Verification
    // requirement: one per FormDeedComparer.Compare field, the same
    // "material field -> named reason" shape MaterialFieldReasons already
    // establishes above, just for the seller's own form data against their
    // own uploaded deed rather than seller-deed-vs-government-deed. ----

    /// <summary>
    /// RETIRED (Owner Name / Owner NIC / Owner Address requirement) - no
    /// longer produced by FormDeedComparer, which now compares the
    /// Property's own explicit OwnerNIC column, never the Seller account's
    /// NIC (see FormDeedComparer's own doc comment for why the substitution
    /// was removed). Kept only so a DeedVerificationReason row persisted
    /// before this requirement still reads back correctly - use
    /// <see cref="FormOwnerNicMismatch"/> for every new comparison.
    /// </summary>
    FormSellerNicMismatch = 14,

    /// <summary>Form-vs-Deed: the listing's explicit Owner Name (Property.OwnerName) did not match the owner name OCR'd from the seller's own uploaded deed.</summary>
    FormOwnerNameMismatch = 15,

    /// <summary>Form-vs-Deed: the listing's Deed Reference (Property.DeedReference) did not match the deed number OCR'd from the seller's own uploaded deed.</summary>
    FormDeedNumberMismatch = 16,

    /// <summary>
    /// RETIRED (Owner Name / Owner NIC / Owner Address requirement) - no
    /// longer produced by FormDeedComparer, which no longer compares
    /// Property.Location at all (that field is a marketing description of
    /// where the land is, not the deed-registered owner's address). Kept
    /// only so a DeedVerificationReason row persisted before this
    /// requirement still reads back correctly - use
    /// <see cref="FormOwnerAddressMismatch"/> for every new comparison.
    /// </summary>
    FormLocationMismatch = 17,

    /// <summary>
    /// RETIRED (Owner Name / Owner NIC / Owner Address requirement) -
    /// FormDeedComparer no longer compares Property.District at all; the
    /// Form-vs-Deed check is now scoped to exactly the 4 explicit
    /// deed-owner identity fields (Owner Name, Owner NIC, Owner Address,
    /// Deed Number). Kept only so a DeedVerificationReason row persisted
    /// before this requirement still reads back correctly.
    /// </summary>
    FormDistrictMismatch = 18,

    /// <summary>
    /// RETIRED (Owner Name / Owner NIC / Owner Address requirement) -
    /// FormDeedComparer no longer compares Property.Size at all - see
    /// <see cref="FormDistrictMismatch"/>'s doc comment. Kept only so a
    /// DeedVerificationReason row persisted before this requirement still
    /// reads back correctly.
    /// </summary>
    FormLandSizeMismatch = 19,

    /// <summary>Form-vs-Deed: the listing's explicit Owner NIC (Property.OwnerNIC) did not match the NIC OCR'd from the seller's own uploaded deed. Replaces the retired <see cref="FormSellerNicMismatch"/> (which compared the Seller account's own NIC instead - no longer done).</summary>
    FormOwnerNicMismatch = 20,

    /// <summary>Form-vs-Deed: the listing's explicit Owner Address (Property.OwnerAddress) did not match the property address OCR'd from the seller's own uploaded deed. Replaces the retired <see cref="FormLocationMismatch"/> (which compared Property.Location instead - no longer done).</summary>
    FormOwnerAddressMismatch = 21,

    /// <summary>Global Duplicate-Property Prevention requirement: the resolved Government Property Reference already belongs to a different PropertyID - see <see cref="DeedVerificationStatus.DuplicateProperty"/>'s own doc comment.</summary>
    DuplicatePropertyReference = 22
}
