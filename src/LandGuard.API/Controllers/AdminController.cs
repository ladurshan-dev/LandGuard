using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Admin-only property moderation - Phase B2 (Admin Property Moderation
/// API). Every action is a thin translation from HTTP to
/// <see cref="IAdminModerationService"/> and back, the same split every
/// other controller in this solution establishes; ownership/role
/// validation, the <c>Property.Status</c> transition, <c>AdminAction</c>
/// history and seller notifications all already happen inside the
/// existing <c>usp_Admin_ApproveProperty</c>/<c>usp_Admin_RejectProperty</c>
/// stored procedures (Module 2, unchanged) and are not duplicated here.
///
/// This is a manual override path, independent of the automatic
/// score-driven <c>Property.Status</c> transition
/// <c>usp_Risk_GenerateReport</c> still performs - see that procedure's
/// own "PHASE B NOTE" comment for why that automatic transition was
/// intentionally left in place rather than replaced by this endpoint.
///
/// <c>[Authorize(Policy = RequireAdmin)]</c> at the class level - every
/// action here requires the same policy, unlike FraudController/
/// PropertyController which mix visibility levels per action.
/// </summary>
[ApiController]
[Route("api/admin/properties")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminModerationService _adminModerationService;
    private readonly ICurrentUserService _currentUserService;

    public AdminController(IAdminModerationService adminModerationService, ICurrentUserService currentUserService)
    {
        _adminModerationService = adminModerationService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// GET /api/admin/properties/review - Admin only. The review queue:
    /// every property genuinely awaiting manual attention (normally
    /// Status = Pending since Phase C, plus any legacy Flagged rows or
    /// anything with an open suspicious report) - see
    /// <see cref="IAdminModerationService.GetReviewQueueAsync"/>'s own doc
    /// comment for exactly what it reads and why. Read-only; does not
    /// change any Property.Status.
    /// </summary>
    [HttpGet("review")]
    public async Task<IActionResult> GetReviewQueue(CancellationToken cancellationToken)
    {
        var result = await _adminModerationService.GetReviewQueueAsync(cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/admin/properties/{propertyId}/approve - Admin only.
    /// <paramref name="request"/> is optional. AdminId always comes from
    /// the caller's JWT, never the request body.
    /// </summary>
    [HttpPost("{propertyId:int}/approve")]
    public async Task<IActionResult> Approve(
        int propertyId, [FromBody] ApprovePropertyRequest? request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _adminModerationService.ApprovePropertyAsync(propertyId, adminId, request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/admin/properties/{propertyId}/reject - Admin only.
    /// AdminId always comes from the caller's JWT, never the request
    /// body.
    /// </summary>
    [HttpPost("{propertyId:int}/reject")]
    public async Task<IActionResult> Reject(
        int propertyId, [FromBody] RejectPropertyRequest request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _adminModerationService.RejectPropertyAsync(propertyId, adminId, request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
