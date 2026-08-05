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
    Rejected = 4
}
