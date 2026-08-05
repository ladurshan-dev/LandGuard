namespace LandGuard.Domain.Enums;

/// <summary>
/// What an administrator did, as recorded in <c>dbo.AdminAction</c>
/// (<c>CK_AdminAction_Type</c>) - the audit trail required by FR09/NFR02.
/// Every admin-initiated stored procedure (<c>usp_Admin_ApproveProperty</c>,
/// <c>usp_Admin_RejectProperty</c>, <c>usp_Admin_SetUserActive</c>,
/// <c>usp_Admin_ResolveReport</c>, <c>usp_Admin_VerifyNIC</c>, ...) inserts
/// exactly one row here with one of these values.
/// </summary>
public enum AdminActionType
{
    ApproveListing = 1,
    RejectListing = 2,
    FlagListing = 3,
    SuspendUser = 4,
    ReactivateUser = 5,
    VerifyNIC = 6,
    ResolveReport = 7,
    RemoveListing = 8
}
