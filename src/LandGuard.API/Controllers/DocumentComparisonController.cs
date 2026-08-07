using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.Fraud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// OCR-based deed comparison - Module 5C. A separate controller from
/// <see cref="FraudController"/> (Module 5A) rather than adding actions to
/// it, so a completed module's file is never touched - but it shares the
/// exact same <c>api/fraud</c> route prefix the brief specifies
/// (POST /api/fraud/compare/{propertyId}, GET
/// /api/fraud/comparison/{propertyId}), which ASP.NET Core routing allows:
/// two controllers may declare the same prefix as long as their action
/// templates don't collide, and "compare"/"comparison" never collide with
/// FraudController's "analyze"/"report"/"history".
///
/// Every action is a thin translation from HTTP to
/// <see cref="IDocumentComparisonService"/> and back - all business logic
/// (ownership/active-seller validation, visibility rules, the comparison
/// itself) lives in DocumentComparisonService, the same split every other
/// controller in this solution follows.
/// </summary>
[ApiController]
[Route("api/fraud")]
[Authorize]
public class DocumentComparisonController : ControllerBase
{
    private readonly IDocumentComparisonService _documentComparisonService;
    private readonly ICurrentUserService _currentUserService;

    public DocumentComparisonController(
        IDocumentComparisonService documentComparisonService, ICurrentUserService currentUserService)
    {
        _documentComparisonService = documentComparisonService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// POST /api/fraud/compare/{propertyId} - Seller (own properties only)
    /// or Admin. Body is the OCR field data already produced by a prior
    /// POST /api/ocr/extract call (Module 5B) - this action does not run
    /// OCR itself. Compares each field against LandGuardDB, persists the
    /// result, and returns it alongside the property's current fraud risk.
    /// </summary>
    [HttpPost("compare/{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    public async Task<IActionResult> Compare(
        int propertyId, [FromBody] DocumentComparisonRequest request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _documentComparisonService.CompareDocumentAsync(
            propertyId, request, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/fraud/comparison/{propertyId} - any authenticated role
    /// (Buyer read-only, Seller, Admin). Same visibility rule as
    /// FraudController.GetReport: visible once the property is Approved;
    /// otherwise only to its owner or an Admin. Returns the most recent
    /// comparison - never re-compares.
    /// </summary>
    [HttpGet("comparison/{propertyId:int}")]
    public async Task<IActionResult> GetLatestComparison(int propertyId, CancellationToken cancellationToken)
    {
        var result = await _documentComparisonService.GetLatestComparisonAsync(
            propertyId, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }
}
