using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over LandGuardDB's admin property-moderation
/// stored procedures (<c>usp_Admin_ApproveProperty</c>/
/// <c>usp_Admin_RejectProperty</c>, Module 2, unchanged) - Phase B2 (Admin
/// Property Moderation API). Implemented in Infrastructure using Dapper
/// (see <c>AdminStoredProcedures</c>), following exactly the shape
/// <c>IPropertyStoredProcedures</c>/<c>IFraudStoredProcedures</c> already
/// establish - Application only ever sees this interface and plain DTOs,
/// never a SQL string or a Dapper type.
///
/// Both procedures already validate the caller is an active Admin,
/// validate the property exists, perform the <c>dbo.Property.Status</c>
/// transition, insert one <c>dbo.AdminAction</c> history row, and insert a
/// seller <c>Notification</c> - none of that is duplicated here or in
/// <c>AdminModerationService</c>. A RAISERROR from either procedure
/// (invalid/inactive admin, property not found) surfaces as a
/// <c>SqlException</c>, already mapped to a clean 400 response by
/// <c>ExceptionHandlingMiddleware</c>, the same pattern
/// <c>usp_User_Register</c>'s RAISERRORs already rely on (Module 3).
///
/// This is a manual override path, independent of the automatic
/// score-driven <c>Property.Status</c> transition
/// <c>usp_Risk_GenerateReport</c> still performs - see that procedure's
/// own "PHASE B NOTE" comment (<c>Database/Scripts/04_StoredProcedures.sql</c>)
/// for why that automatic transition was intentionally left in place
/// rather than replaced by this one.
/// </summary>
public interface IAdminStoredProcedures
{
    /// <summary>Wraps usp_Admin_ApproveProperty. Returns the refreshed vw_PropertyListing row for propertyId (the procedure's own final SELECT).</summary>
    Task<PropertyListingResult> ApprovePropertyAsync(
        int adminId, int propertyId, string? remarks, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_Admin_RejectProperty. Returns the refreshed vw_PropertyListing row for propertyId (the procedure's own final SELECT).</summary>
    Task<PropertyListingResult> RejectPropertyAsync(
        int adminId, int propertyId, string? remarks, CancellationToken cancellationToken = default);
}
