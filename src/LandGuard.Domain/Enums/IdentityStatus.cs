namespace LandGuard.Domain.Enums;

/// <summary>
/// Seller Government Identity Verification requirement. Distinct from
/// <c>User.NicVerified</c> (a plain bool - cannot represent three states,
/// which is why this enum exists at all, to distinguish "not yet checked"
/// from "checked and disagreed" without ever conflating the two).
/// NicVerified is NOT left independent, though: <c>usp_User_SetIdentityStatus</c>
/// (the sole writer of this status) keeps it in lockstep in the same
/// UPDATE - Verified maps to NicVerified = 1, Pending/Failed both map to 0 -
/// because NicVerified is still a live signal read by the legacy fraud
/// engine's CHECK 3 and surfaced to Buyers/Admins as the "(NIC verified)"
/// badge. See that stored procedure's own comment for the full reasoning.
///
/// Only ever set for a Seller - a Buyer's registration never triggers an
/// identity check at all, so a Buyer/Admin row's <c>IdentityStatus</c>
/// stays NULL (no C# member represents "not applicable"; the database
/// column is nullable for exactly that reason).
/// </summary>
public enum IdentityStatus
{
    /// <summary>No registry answer yet - either never checked, or the last check failed TECHNICALLY (registry unavailable, timeout, unexpected exception). Never implies a name/NIC mismatch. May list a property only once this becomes Verified.</summary>
    Pending = 1,

    /// <summary>The registry returned an Active record for this NIC whose normalized name matches the Seller's own registered name exactly.</summary>
    Verified = 2,

    /// <summary>The registry authoritatively answered "no" - no record for this NIC, a record whose name does not match, or a found record that is not active/cancelled (if the dummy/real registry represents that state). Never a technical failure - see <see cref="Pending"/>.</summary>
    Failed = 3
}
