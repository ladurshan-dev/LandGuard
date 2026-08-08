using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Common.Interfaces.StoredProcedures;

/// <summary>
/// Application-layer contract over the Phase 5B persistence procedures
/// (<c>usp_DeedVerification_Create</c>, <c>usp_DeedVerificationField_Add</c>,
/// <c>usp_DeedVerificationReason_Add</c>, <c>usp_DeedVerification_GetHistory</c>)
/// - following the exact per-area wrapper shape
/// <c>IFraudStoredProcedures</c>/<c>IPropertyStoredProcedures</c> already
/// establish.
///
/// <see cref="PersistAsync"/> is deliberately a single atomic operation,
/// not three separate Create/AddField/AddReason methods. An earlier
/// revision of this interface exposed them separately, which required
/// <c>GovernmentDeedVerificationService</c> (Application) to hold an EF
/// Core transaction open across all three calls via a
/// <c>DatabaseFacade</c> property added to <c>IApplicationDbContext</c> -
/// corrected, because the Application layer must never depend on EF Core
/// transaction types merely to sequence Dapper calls. Application now only
/// ever says "persist this verification result"; this interface's shape
/// makes that the only thing it *can* say - it accepts/returns plain DTOs
/// and primitives only, nothing from <c>Microsoft.EntityFrameworkCore</c>
/// or <c>Dapper</c> is reachable through it. Transaction ownership
/// (BEGIN/COMMIT/ROLLBACK) lives entirely inside the Infrastructure
/// implementation, <c>GovernmentDeedVerificationStoredProcedures</c> - see
/// that class's own doc comment for exactly how.
///
/// No update/delete method exists here, deliberately - <c>dbo.DeedVerification</c>
/// and its two child tables are append-only (see the Domain
/// <c>DeedVerification</c> entity's own doc comment); a corrected
/// re-verification calls <see cref="PersistAsync"/> again, it never edits
/// a past row.
/// </summary>
public interface IGovernmentDeedVerificationStoredProcedures
{
    /// <summary>
    /// Inserts one <c>DeedVerification</c> row plus every
    /// <c>DeedVerificationField</c>/<c>DeedVerificationReason</c> row
    /// <paramref name="result"/> carries, as a single atomic database
    /// operation - all committed together, or entirely rolled back if any
    /// part fails. Returns the new <c>DeedVerificationID</c>.
    /// </summary>
    /// <param name="result">The already-computed classification to persist - never recomputed here.</param>
    /// <param name="submittedByUserId">The caller who submitted the seller deed for verification (Seller or Admin) - resolved server-side, never trusted from a request body.</param>
    Task<int> PersistAsync(
        GovernmentDeedFraudDetectionResult result, int submittedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Wraps usp_DeedVerification_GetHistory's 3 result sets. Every past verification run for a property, newest first, with its field evidence and reasons grouped underneath. Needed now so Phase 5C can display verification history without a new stored-procedure wrapper later.</summary>
    Task<IReadOnlyList<DeedVerificationHistoryEntry>> GetHistoryAsync(
        int propertyId, CancellationToken cancellationToken = default);
}
