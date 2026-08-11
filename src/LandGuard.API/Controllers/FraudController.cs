using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Fraud analysis, reporting and history - Module 5A. Every action is a
/// thin translation from HTTP to <see cref="IFraudDetectionService"/> and
/// back; all business logic (ownership/active-seller validation,
/// visibility rules) lives in FraudDetectionService, the same split
/// AuthController/PropertyController established.
///
/// Every endpoint requires a valid JWT - there is no anonymous access
/// here, unlike PropertyController's public GetById/Search. All three
/// actions are gated to Seller-or-Admin by
/// <see cref="AuthorizationPolicies.RequireSellerOrAdmin"/> at the
/// attribute level; Seller-can-only-analyze/read-their-own-properties
/// (Analyze) or Seller-owner-or-Admin (GetReport/GetHistory, which also
/// allow the owning Seller to read an Approved listing that started as
/// someone else's - not applicable in practice since a Seller only ever
/// owns their own listings) is enforced inside FraudDetectionService,
/// which is the only place that actually knows who owns which property.
///
/// BUYER PRIVACY REQUIREMENT: GetReport/GetHistory used to also allow
/// Buyer (bare <c>[Authorize]</c>, "read-only" access) - removed. Internal
/// fraud-engine output (score, risk level, fraud status, rule-by-rule
/// breakdown) must never reach a Buyer, even for an Approved listing;
/// Approval is sufficient information for them. A Buyer viewing a
/// property's normal listing details still works via
/// PropertyController.GetById, which now separately redacts the same
/// fields for a non-owner, non-Admin caller (see
/// PropertyService.GetByIdAsync) - this controller no longer needs to
/// reason about a Buyer caller at all.
/// </summary>
[ApiController]
[Route("api/fraud")]
[Authorize]
public class FraudController : ControllerBase
{
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly ICurrentUserService _currentUserService;

    public FraudController(IFraudDetectionService fraudDetectionService, ICurrentUserService currentUserService)
    {
        _fraudDetectionService = fraudDetectionService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// POST /api/fraud/analyze/{propertyId} - Seller (own properties only)
    /// or Admin. Triggers usp_Fraud_AnalyseProperty (unchanged from
    /// Module 2/4) and returns the resulting score and rule breakdown.
    /// </summary>
    [HttpPost("analyze/{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    public async Task<IActionResult> Analyze(int propertyId, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _fraudDetectionService.AnalyzePropertyAsync(
            propertyId, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/fraud/report/{propertyId} - Seller (own properties) or
    /// Admin only. No longer Buyer-accessible (Buyer privacy requirement -
    /// see this controller's own doc comment).
    /// </summary>
    [HttpGet("report/{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    public async Task<IActionResult> GetReport(int propertyId, CancellationToken cancellationToken)
    {
        var result = await _fraudDetectionService.GetFraudReportAsync(
            propertyId, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/fraud/history/{propertyId} - Seller (own properties) or
    /// Admin only. No longer Buyer-accessible - same change as GetReport,
    /// see this controller's own doc comment.
    /// </summary>
    [HttpGet("history/{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    public async Task<IActionResult> GetHistory(int propertyId, CancellationToken cancellationToken)
    {
        var result = await _fraudDetectionService.GetFraudHistoryAsync(
            propertyId, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }
}
