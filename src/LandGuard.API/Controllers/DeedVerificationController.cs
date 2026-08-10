using LandGuard.API.Authorization;
using LandGuard.API.Models;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Government Registry module, Phase 5C - the end-to-end verification
/// endpoint. A thin translation from HTTP to
/// <see cref="IGovernmentDeedVerificationService.VerifyAndPersistAsync"/>
/// and back, the same split every other controller in this solution
/// establishes; comparison (Phase 4), classification (Phase 5A) and
/// persistence (Phase 5B) all happen inside that one call, unmodified -
/// nothing here re-implements or duplicates any of it.
///
/// Deliberately a new, separate endpoint
/// (<c>POST /api/deed-verification/{propertyId}</c>) rather than a change
/// to <c>POST /api/deed-comparison/{propertyId}</c>
/// (<see cref="DeedComparisonController"/>, unmodified, still
/// comparison-only with no persistence) - that endpoint's already-tested
/// behavior (GR-000001 through GR-000006) must not change.
///
/// [Authorize(Policy = RequireSellerOrAdmin)] - the same policy
/// <see cref="DeedComparisonController.Compare"/> uses, for the same
/// reason: this action also accepts a multipart deed upload for the same
/// caller population, and nothing in this phase's approved design extends
/// that to Buyer. Ownership beyond role (a Seller may only verify their
/// own property; an Admin may verify any) is enforced entirely inside
/// <c>GovernmentDeedComparisonService.CompareAsync</c> via the
/// <c>callerId</c>/<c>callerRole</c> this action passes through - see
/// <c>GovernmentDeedVerificationService.VerifyAndPersistAsync</c>'s own doc
/// comment for why nothing here re-checks it a second time.
///
/// <c>callerId</c> always comes from <see cref="ICurrentUserService"/>
/// (server-resolved from JWT claims) - never from the request body or
/// form - and is the exact value
/// <c>GovernmentDeedVerificationStoredProcedures.PersistAsync</c> writes as
/// <c>DeedVerification.SubmittedByUserID</c>.
/// </summary>
[ApiController]
[Route("api/deed-verification")]
public class DeedVerificationController : ControllerBase
{
    private readonly IGovernmentDeedVerificationService _governmentDeedVerificationService;
    private readonly ICurrentUserService _currentUserService;

    public DeedVerificationController(
        IGovernmentDeedVerificationService governmentDeedVerificationService, ICurrentUserService currentUserService)
    {
        _governmentDeedVerificationService = governmentDeedVerificationService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// POST /api/deed-verification/{propertyId} - Seller (own properties
    /// only) or Admin. Accepts the seller's deed PDF/scan for
    /// <paramref name="propertyId"/>, runs the full
    /// comparison -&gt; classification -&gt; persistence pipeline, and returns
    /// the persisted verdict as a <see cref="DeedVerificationResponse"/>.
    /// </summary>
    [HttpPost("{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> Verify(
        int propertyId, [FromForm] OcrUploadRequest request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }

        await using var stream = file.OpenReadStream();

        var result = await _governmentDeedVerificationService.VerifyAndPersistAsync(
            propertyId, file.FileName, file.ContentType, stream, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(DeedVerificationResponse.FromOutcome(result.Data!))
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/deed-verification/{propertyId} - Seller (own properties
    /// only) or Admin. Reads every already-persisted verification run for
    /// <paramref name="propertyId"/> (newest first), without running a new
    /// one - the read counterpart to <see cref="Verify"/> above (Phase D:
    /// lets the Seller Property Details and Admin Review pages display a
    /// verification that already ran, instead of only ever showing a
    /// session-only result from the action above).
    /// </summary>
    [HttpGet("{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    public async Task<IActionResult> GetHistory(int propertyId, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _governmentDeedVerificationService.GetHistoryAsync(
            propertyId, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data!.Select(DeedVerificationResponse.FromHistoryEntry).ToList())
            : BadRequest(new { errors = result.Errors });
    }
}
