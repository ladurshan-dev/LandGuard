namespace LandGuard.Application.Common.Models;

/// <summary>
/// Contact Seller workflow: the smallest possible DTO returned by
/// <c>GET /api/properties/{id}/seller-contact</c>
/// (<see cref="IPropertyService.GetSellerContactAsync"/>), deliberately kept
/// separate from <see cref="PropertyListingResult"/>/<see cref="PropertySearchResult"/>
/// rather than un-redacting <c>SellerPhone</c> on those - a Buyer must
/// explicitly request contact information via this dedicated endpoint
/// before receiving it (see PropertyService.GetSellerContactAsync's own doc
/// comment for the Approved-only gate), never receive it as part of the
/// general property read.
///
/// Deliberately excludes SellerID, Seller NIC, Owner NIC, Owner Address,
/// DeedReference, GovernmentPropertyReference, and any fraud/risk/OCR/
/// verification-history data - none of that is needed to contact a Seller,
/// and returning it here would just reopen the same privacy leak this
/// endpoint exists to close.
/// </summary>
public class SellerContactInfo
{
    public string SellerName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Sourced from Users.NICVerified (kept in lockstep with IdentityStatus - see IdentityStatus's own doc comment), the same "Verified Seller" signal already shown, unredacted, on the property listing itself.</summary>
    public bool VerifiedSeller { get; set; }
}
