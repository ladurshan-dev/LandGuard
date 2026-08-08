using LandGuard.API.Authorization;
using LandGuard.API.Models;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Government Registry module, Phase 4 - compares a seller's uploaded deed
/// against the trusted government record for one of their properties.
/// Every action here is a thin translation from HTTP to
/// <see cref="IGovernmentDeedComparisonService"/> and back, the same split
/// every other controller in this solution establishes; no OCR, storage,
/// registry-lookup or comparison logic lives here.
///
/// Bound as a single <see cref="OcrUploadRequest"/> model (reused as-is,
/// not duplicated - this endpoint accepts exactly the same "one file"
/// shape <c>POST /api/ocr/extract</c> already does), so the seller's deed
/// arrives as an actual uploaded file, never as a JSON object of claimed
/// field values - <see cref="IGovernmentDeedComparisonService.CompareAsync"/>
/// only ever builds its seller-side data by OCR'ing this upload.
///
/// [Authorize(Policy = RequireSellerOrAdmin)] - the same policy
/// <c>OcrController.Extract</c> uses, deliberately excluding Buyer: this
/// endpoint both accepts a multipart deed upload and returns
/// government-record fields (NIC, owner name, address), and nothing in
/// the approved Phase 4 design justifies giving Buyers that capability.
/// Ownership beyond role (a Seller may only compare their own property;
/// an Admin may compare any) is enforced inside
/// <see cref="IGovernmentDeedComparisonService.CompareAsync"/> itself,
/// which throws <c>NotFoundException</c>/<c>UnauthorizedAccessException</c>
/// for a nonexistent or not-owned <c>propertyId</c> - both already mapped
/// to 404/403 by <c>ExceptionHandlingMiddleware</c>, so this action never
/// needs its own ownership check.
/// </summary>
[ApiController]
[Route("api/deed-comparison")]
public class DeedComparisonController : ControllerBase
{
    private readonly IGovernmentDeedComparisonService _governmentDeedComparisonService;
    private readonly ICurrentUserService _currentUserService;

    public DeedComparisonController(
        IGovernmentDeedComparisonService governmentDeedComparisonService, ICurrentUserService currentUserService)
    {
        _governmentDeedComparisonService = governmentDeedComparisonService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// POST /api/deed-comparison/{propertyId} - Seller (own properties
    /// only) or Admin. Accepts the seller's deed PDF/scan for
    /// <paramref name="propertyId"/>, OCRs it via the existing pipeline,
    /// resolves the trusted government record, and returns a
    /// <c>GovernmentDeedComparisonReport</c> - a separate result from the
    /// existing fraud report, not merged into it (see that DTO's doc
    /// comment for why).
    /// </summary>
    [HttpPost("{propertyId:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> Compare(
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

        var result = await _governmentDeedComparisonService.CompareAsync(
            propertyId, file.FileName, file.ContentType, stream, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
