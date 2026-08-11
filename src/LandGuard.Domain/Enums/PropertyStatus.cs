namespace LandGuard.Domain.Enums;

/// <summary>
/// Lifecycle state of a property listing - the backbone of LandGuard's core
/// workflow. Redefined in Module 2 to match the four values actually
/// enforced by the LandGuardDB schema (<c>CK_Property_Status</c> in
/// <c>dbo.Property</c>) rather than the five speculative states drafted in
/// Module 1 before the database existed. There is no separate "under
/// analysis" state in the database - fraud analysis runs synchronously
/// inside <c>usp_Fraud_AnalyseProperty</c> during the same call that
/// inserts the property, so a row is never observably "mid-analysis".
/// There is also no "Suspended" listing state: suspension in this schema
/// applies to a <em>user</em> account (<c>Users.IsActive</c>), not to an
/// individual listing.
/// </summary>
public enum PropertyStatus
{
    /// <summary>Submitted and awaiting (or between) fraud analysis runs.</summary>
    Pending = 1,

    /// <summary>Published and visible to Buyers - reached automatically for Low risk, or via admin approval otherwise.</summary>
    Approved = 2,

    /// <summary>Medium/High risk on the latest analysis run; waiting in the admin review queue.</summary>
    Flagged = 3,

    /// <summary>Rejected by an admin. The seller may edit and resubmit, which resets status to Pending.</summary>
    Rejected = 4,

    /// <summary>
    /// Voluntarily withdrawn by the owning Seller (Phase F). Not a fraud
    /// verdict - a listing lifecycle state only. Removed from Buyer
    /// browsing and the Admin review queue, but the property row and every
    /// piece of DeedVerification/FraudCheck/RiskReport/AdminAction/
    /// Notification history is preserved untouched. Not reachable through
    /// the normal edit flow (usp_Property_Update refuses to touch a
    /// Withdrawn property) - there is no "Relist" action yet.
    /// </summary>
    Withdrawn = 5,

    /// <summary>
    /// SYSTEM-AUTOMATED outcome of the Mandatory Deed / Form-vs-Deed
    /// Verification requirement - reached automatically by
    /// <c>usp_Property_ApplyDeedVerificationOutcome</c> when
    /// GovernmentDeedFraudDetectionService.Classify produces
    /// <c>DeedVerificationStatus.FormMismatch</c> (the seller-entered
    /// listing fields do not match their own uploaded deed) or
    /// <c>DeedVerificationStatus.Fraudulent</c> (the uploaded deed does not
    /// match the trusted Government Registry record). Deliberately a
    /// distinct value from <see cref="Rejected"/>, which stays exactly what
    /// it already meant - a manual Admin decision, with its own
    /// AdminAction row and "reviewed and rejected by an administrator"
    /// notification wording, neither of which would be true for this
    /// automated case. Never sent to the normal Admin price-anomaly review
    /// queue (vw_FlaggedProperty's own WHERE clause only ever matches
    /// 'Flagged'/'Pending'), never visible to a Buyer (vw_PublishedProperty/
    /// usp_Property_Search only ever return 'Approved'). The Seller may
    /// still edit the listing - usp_Property_Update resets any edit to
    /// 'Pending' unconditionally (the same reset every other non-Withdrawn
    /// status already goes through), which is how a corrected listing
    /// re-enters verification rather than remaining stuck.
    /// </summary>
    Disapproved = 6
}
