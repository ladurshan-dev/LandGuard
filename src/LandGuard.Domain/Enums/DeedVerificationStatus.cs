namespace LandGuard.Domain.Enums;

/// <summary>
/// Outcome of classifying a <c>GovernmentDeedComparisonReport</c> (Government
/// Registry module, Phase 4) into a verdict about the uploaded DOCUMENT's
/// trusted-registry verification - Phase 5A. Deliberately its own enum, not
/// a reuse of <see cref="FraudStatus"/>: <c>FraudStatus</c> is the verdict
/// the numeric, T-SQL-computed fraud engine writes onto
/// <c>dbo.FraudCheck.FraudStatus</c> (Clean/Suspicious/Fraudulent, derived
/// from a weighted point score); this enum is produced entirely in C# by
/// <c>GovernmentDeedFraudDetectionService</c> from a field-by-field diff
/// against a trusted government record, and is not written to
/// <c>dbo.FraudCheck</c> or any other existing fraud table - the two
/// systems are intentionally independent (see that service's doc comment).
///
/// <b><see cref="Fraudulent"/> describes the uploaded DOCUMENT failing
/// trusted-registry verification, never the seller/account.</b> Nothing in
/// this enum, or in the service that produces it, marks a
/// <c>User</c>/Seller as fraudulent - that remains an Admin's own,
/// separate determination (see <c>GovernmentDeedFraudDetectionService</c>'s
/// doc comment for the full reasoning).
/// </summary>
public enum DeedVerificationStatus
{
    /// <summary>The government record is Active and every material field, plus price, was within tolerance - no evidence of a problem with the uploaded document.</summary>
    Verified = 1,

    /// <summary>At least one MATERIAL field (NIC, OwnerName, DeedNumber, PropertyReference, LandSize, District, Address, RegistrationDate) disagrees with the trusted government record - see <c>GovernmentDeedFraudDetectionService</c> for the exact field list and rationale.</summary>
    Fraudulent = 2,

    /// <summary>Every material field matched; only the asking-price-vs-registered-price check exceeded its anomaly threshold. Deliberately never classified as <see cref="Fraudulent"/> - see <c>DeedFieldComparer.ComparePrice</c>'s own doc comment for why these two prices are expected to differ even for a genuine listing.</summary>
    PriceAnomaly = 3,

    /// <summary>No trusted government record could be resolved at all, or one was resolved but its trusted document could not be retrieved for comparison - either way, there is nothing reliable to verify the upload against.</summary>
    Unverified = 4,

    /// <summary>A trusted government record was resolved, but the government no longer currently recognises it as valid (Status other than "Active" - see <c>GovernmentLandRecordDto.Status</c>'s own doc comment, which already groups Cancelled/Suspended together as "no longer recognised as currently valid"). Kept distinct from the bare <see cref="Unverified"/> case because the reason verification failed is more specific and worth surfacing separately.</summary>
    UnverifiedCancelled = 5
}
