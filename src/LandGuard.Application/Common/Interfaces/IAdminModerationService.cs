using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Admin;
using LandGuard.Domain.ReadModels;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Admin property moderation - Phase B2 (Admin Property Moderation API).
/// A thin orchestrator over
/// <see cref="StoredProcedures.IAdminStoredProcedures"/>, the same
/// "Application only ever composes calls, never contains SQL" split every
/// other service in this solution uses (AuthService, PropertyService,
/// FraudDetectionService, GovernmentDeedVerificationService).
///
/// This is a manual override path that exists alongside - and does not
/// replace - the automatic score-driven <c>Property.Status</c> transition
/// <c>usp_Risk_GenerateReport</c> still performs. See that procedure's own
/// "PHASE B NOTE" comment for why that automatic transition was
/// intentionally left in place: no other mechanism reachable from the
/// running application currently promotes a property out of
/// <c>Pending</c>, so removing it without this endpoint existing first
/// would have left every property stuck. This service is that missing
/// mechanism for the manual path; the automatic path is unchanged.
/// </summary>
public interface IAdminModerationService
{
    /// <summary>
    /// Approves a property, wrapping <c>usp_Admin_ApproveProperty</c>. The
    /// procedure itself validates the caller is an active Admin (defense
    /// in depth alongside the <c>RequireAdmin</c> authorization policy
    /// already enforced at the controller) and that the property exists -
    /// neither check is duplicated here.
    /// </summary>
    Task<Result<PropertyListingResult>> ApprovePropertyAsync(
        int propertyId, int adminId, ApprovePropertyRequest? request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a property, wrapping <c>usp_Admin_RejectProperty</c>.
    /// <see cref="RejectPropertyRequest.Reason"/> is validated as required
    /// at this layer before the stored procedure is even called - see
    /// <c>RejectPropertyRequestValidator</c>.
    /// </summary>
    Task<Result<PropertyListingResult>> RejectPropertyAsync(
        int propertyId, int adminId, RejectPropertyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The admin review queue - every property genuinely awaiting manual
    /// attention. Reads <c>dbo.vw_FlaggedProperty</c> (the same view
    /// <c>usp_Admin_GetFlagged</c> reads) directly through
    /// <see cref="IApplicationDbContext"/> rather than a new Dapper
    /// stored-procedure wrapper: that view is already mapped as a
    /// keyless EF Core read model
    /// (<see cref="LandGuard.Domain.ReadModels.FlaggedProperty"/>,
    /// <c>IApplicationDbContext.FlaggedProperties</c>) with no existing
    /// caller, so this is the smallest addition that reuses what already
    /// exists instead of duplicating the view's own filter logic in C#.
    /// Despite its name (unchanged, to avoid touching the view itself),
    /// the view already includes <c>Status = 'Pending'</c> rows - the
    /// normal review state since Phase C - alongside any legacy
    /// <c>Flagged</c> rows and anything with an open suspicious report.
    /// </summary>
    Task<Result<IReadOnlyList<FlaggedProperty>>> GetReviewQueueAsync(CancellationToken cancellationToken = default);
}
