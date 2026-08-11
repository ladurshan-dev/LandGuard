using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Services;

/// <summary>
/// Stateless field-by-field diff between the explicit deed-owner fields the
/// Seller typed directly onto the LandGuard listing (<c>Property.OwnerName</c>/
/// <c>OwnerNIC</c>/<c>OwnerAddress</c>/<c>DeedReference</c> - Owner Name /
/// Owner NIC / Owner Address requirement) and the same seller's own uploaded
/// deed, as already OCR'd into a <see cref="SellerDeedData"/> by
/// <c>GovernmentDeedComparisonService.MapToSellerDeedData</c> - the
/// Mandatory Deed / Form-vs-Deed Verification requirement's "does the form
/// match the seller's own deed" check, run BEFORE any Government Registry
/// lookup is attempted (see <c>GovernmentDeedComparisonService.CompareAsync</c>).
///
/// CORRECTED: this no longer reads the Seller account's own <c>User.Name</c>/
/// <c>User.Nic</c> as stand-ins for "Owner Name"/"Owner NIC", and no longer
/// reads <c>Property.Location</c> as a stand-in for "Owner Address" - the
/// Owner Name / Owner NIC / Owner Address requirement added explicit
/// <c>Property</c> columns for exactly this data, so the substitution this
/// class used to need is gone. The Seller account still identifies who
/// manages the listing; it is simply no longer read by this comparison.
/// District and Land Size are deliberately no longer compared here either -
/// the Form-vs-Deed check is now scoped to exactly the 4 explicit
/// deed-owner identity fields the requirement names (Owner Name, Owner NIC,
/// Owner Address, Deed Number); see <c>DeedFraudReason.FormDistrictMismatch</c>/
/// <c>FormLandSizeMismatch</c>'s own doc comments for the retired fields
/// this replaces.
///
/// Deliberately its own pure, stateless, dependency-free class rather than
/// a new overload on <c>DeedFieldComparer</c>: this compares two
/// structurally different sources (a <c>PropertyListingResult</c> vs. a
/// normalized deed DTO), not two <see cref="SellerDeedData"/>/
/// <see cref="GovernmentDeedData"/> instances, and the two checks answer
/// genuinely different questions (form-vs-own-deed here; own-deed-vs-
/// government-deed there) that the requirement itself keeps sequentially
/// separate. Mirrors <c>DeedFieldComparer</c>'s exact comparison style
/// (trim/collapse whitespace + case-insensitive text compare,
/// "insufficient data on either side ⇒ Match = true" convention - an OCR
/// extraction miss is not evidence of a mismatch) so results read
/// identically to a human reviewer regardless of which comparison produced
/// them.
///
/// Reuses <see cref="DeedFieldComparisonResult"/>'s exact shape rather than
/// introducing a parallel DTO/UI (both already render an arbitrary
/// FieldName/value-pair/Match/Message list generically - see
/// <c>DeedVerificationResultView.tsx</c>) - by convention here,
/// <see cref="DeedFieldComparisonResult.GovernmentValue"/> holds the value
/// extracted from the seller's own uploaded deed (the trusted per-document
/// reference point for THIS comparison) and
/// <see cref="DeedFieldComparisonResult.SellerValue"/> holds the
/// corresponding value entered on the LandGuard listing form - each
/// <see cref="DeedFieldComparisonResult.FieldName"/> is prefixed "Form"
/// (e.g. "FormOwnerNIC") specifically so this reuse is never ambiguous with
/// a government-comparison Evidence entry when both are read back out of
/// history.
/// </summary>
internal static class FormDeedComparer
{
    /// <summary>
    /// Runs every Form-vs-Deed comparison and returns one result per field,
    /// in a fixed order - exactly the 4 explicit deed-owner identity fields
    /// (Owner Name / Owner NIC / Owner Address / Deed Number requirement).
    /// Every parameter here is itself mandatory on <c>Property</c> going
    /// forward (see <c>CreatePropertyRequestValidator</c>), but is typed
    /// nullable/optional-looking (<c>string?</c>) because a Property row
    /// created before this requirement existed can still have NULL here -
    /// see <c>LandGuard.Domain.Entities.Property.OwnerName</c>'s own doc
    /// comment for why the column itself stays nullable. A blank/missing
    /// value on either side is reported as insufficient-data (Match =
    /// true), never as a mismatch, matching <c>CompareText</c>'s existing
    /// convention.
    /// </summary>
    public static IReadOnlyList<DeedFieldComparisonResult> Compare(
        string? propertyOwnerName,
        string? propertyOwnerNic,
        string? propertyOwnerAddress,
        string? propertyDeedReference,
        SellerDeedData sellerDeed) =>
        new List<DeedFieldComparisonResult>
        {
            CompareText("FormOwnerNIC", sellerDeed.Nic, propertyOwnerNic),
            CompareText("FormOwnerName", sellerDeed.OwnerName, propertyOwnerName),
            CompareText("FormOwnerAddress", sellerDeed.Address, propertyOwnerAddress),
            CompareText("FormDeedNumber", sellerDeed.DeedNumber, propertyDeedReference)
        };

    private static DeedFieldComparisonResult CompareText(string fieldName, string? deedValue, string? formValue)
    {
        var normalizedDeed = Normalize(deedValue);
        var normalizedForm = Normalize(formValue);

        if (normalizedDeed is null || normalizedForm is null)
        {
            return Result(fieldName, deedValue, formValue, true,
                "Could not be compared - the value was not found by OCR on the deed, or was left blank on the listing.");
        }

        var match = string.Equals(normalizedDeed, normalizedForm, StringComparison.OrdinalIgnoreCase);

        return Result(fieldName, deedValue, formValue, match,
            match ? "Matches the uploaded deed." : $"Does not match the uploaded deed ({fieldName} mismatch).");
    }

    private static DeedFieldComparisonResult Result(
        string fieldName, string? governmentValue, string? sellerValue, bool match, string message) => new()
    {
        FieldName = fieldName,
        GovernmentValue = governmentValue,
        SellerValue = sellerValue,
        Match = match,
        Message = message
    };

    /// <summary>Trims and collapses internal whitespace runs to a single space; null/whitespace-only becomes null so callers can treat "not found/blank" uniformly. Identical to DeedFieldComparer.Normalize.</summary>
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
