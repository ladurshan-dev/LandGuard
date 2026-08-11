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

    /// <summary>
    /// No trusted government record could be resolved at all, or one was
    /// resolved but its trusted document could not be retrieved/read for
    /// comparison ("Invalid deed" - the one document this verification is
    /// supposed to vouch for could not itself be validated). Despite the
    /// name, this is NOT a technical failure: <c>IGovernmentRegistryService</c>'s
    /// own doc comment is explicit that it "returns null - never throws -
    /// when no record matches," so reaching this status means the registry
    /// was queried successfully and gave a definitive negative answer. A
    /// genuine technical failure (registry service unavailable, network
    /// error, timeout, unexpected API failure) is a thrown exception from
    /// that interface instead, which propagates out of
    /// <c>GovernmentDeedComparisonService.CompareAsync</c> before this
    /// status - or any status - is ever produced; Property.Status is left
    /// untouched in that case. Because this status is always an
    /// authoritative negative finding rather than a technical failure, it
    /// maps to <c>PropertyStatus.Disapproved</c> automatically, the same as
    /// <see cref="Fraudulent"/> - see <c>GovernmentDeedVerificationService.
    /// VerifyAndPersistAsync</c>.
    /// </summary>
    Unverified = 4,

    /// <summary>
    /// A trusted government record was resolved, but the government no
    /// longer currently recognises it as valid (Status other than "Active"
    /// - see <c>GovernmentLandRecordDto.Status</c>'s own doc comment, which
    /// already groups Cancelled/Suspended together as "no longer
    /// recognised as currently valid"). Kept distinct from the bare
    /// <see cref="Unverified"/> case because the reason verification
    /// failed is more specific and worth surfacing separately - but, like
    /// <see cref="Unverified"/>, this is a successful, authoritative
    /// negative registry answer, never a technical failure (see
    /// <see cref="Unverified"/>'s own doc comment for why), so it also
    /// maps to <c>PropertyStatus.Disapproved</c> automatically.
    /// </summary>
    UnverifiedCancelled = 5,

    /// <summary>
    /// The seller-entered listing fields (owner name, NIC, deed reference,
    /// location, district, land size - see <c>FormDeedComparer</c>) do not
    /// match the seller's OWN uploaded deed. This is a statement about the
    /// FORM the seller typed into LandGuard disagreeing with the document
    /// they themselves uploaded - a different, earlier check than
    /// <see cref="Fraudulent"/> (which compares that same uploaded deed
    /// against the independent, trusted Government Registry record).
    /// Decided before any Government Registry lookup is attempted -
    /// <c>GovernmentDeedComparisonService.CompareAsync</c> short-circuits on
    /// this outcome, so <c>GovernmentDeedFraudDetectionResult.Evidence</c>
    /// carries the Form-vs-Deed field comparisons instead of a government
    /// comparison (each <c>FieldName</c> prefixed "Form", e.g.
    /// "FormOwnerNIC" - see <c>FormDeedComparer</c>), never both. Maps to
    /// <c>PropertyStatus.Disapproved</c> automatically - see that enum's
    /// own doc comment.
    /// </summary>
    FormMismatch = 6,

    /// <summary>
    /// Global Duplicate-Property Prevention requirement. Every material
    /// Government Registry field matched (or only price differed), but the
    /// resolved <c>GovernmentLandRecordDto.PropertyReference</c> already
    /// belongs to a DIFFERENT PropertyID
    /// (<c>usp_Property_FindByGovernmentPropertyReference</c>) - the same
    /// legal parcel has already been listed once in LandGuard, under a
    /// different Property record (same Seller or a different one). Takes
    /// priority over <see cref="PriceAnomaly"/> (a duplicate is a material,
    /// non-price problem) but is only ever checked once no MATERIAL field
    /// mismatch was already found - see
    /// <c>GovernmentDeedComparisonService.CompareAsync</c>'s own inline
    /// comment for exactly where. Maps to
    /// <c>PropertyStatus.Disapproved</c>, the same as every other
    /// automated verification failure - see that enum's own doc comment.
    /// The Seller-facing message never names the other PropertyID or its
    /// owner - see <c>usp_Property_ApplyDeedVerificationOutcome</c>'s own
    /// 'DuplicateProperty' branch.
    /// </summary>
    DuplicateProperty = 7
}
