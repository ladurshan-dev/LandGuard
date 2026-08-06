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
/// here, unlike PropertyController's public GetById/Search. Role
/// enforcement is two-layered, matching the spec exactly: Buyer is
/// read-only, so only the two GET actions allow it (the POST is gated to
/// Seller/Admin by <see cref="AuthorizationPolicies.RequireSellerOrAdmin"/>
/// at the attribute level); Seller-can-only-analyze-their-own-properties
/// and Admin-has-full-access are both enforced inside
/// FraudDetectionService, which is the only place that actually knows who
/// owns which property.
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
    /// GET /api/fraud/report/{propertyId} - any authenticated role
    /// (Buyer read-only, Seller, Admin). Visible once the property is
    /// Approved; otherwise only to its owner or an Admin.
    /// </summary>
    [HttpGet("report/{propertyId:int}")]
    public async Task<IActionResult> GetReport(int propertyId, CancellationToken cancellationToken)
    {
        var result = await _fraudDetectionService.GetFraudReportAsync(
            propertyId, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/fraud/history/{propertyId} - any authenticated role
    /// (Buyer read-only, Seller, Admin). Same visibility rule as
    /// GetReport.
    /// </summary>
    [HttpGet("history/{propertyId:int}")]
    public async Task<IActionResult> GetHistory(int propertyId, CancellationToken cancellationToken)
    {
        var result = await _fraudDetectionService.GetFraudHistoryAsync(
            propertyId, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }
}
