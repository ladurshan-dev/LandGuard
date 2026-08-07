using LandGuard.API.Authorization;
using LandGuard.API.Models;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Document OCR extraction - Module 5B. Text extraction only; no fraud
/// scoring or comparison happens here or in
/// <see cref="IOcrDocumentService"/> - see that interface's doc comment
/// for how Module 5C is expected to consume the result.
/// </summary>
[ApiController]
[Route("api/ocr")]
public class OcrController : ControllerBase
{
    private readonly IOcrDocumentService _ocrDocumentService;
    private readonly ICurrentUserService _currentUserService;

    public OcrController(IOcrDocumentService ocrDocumentService, ICurrentUserService currentUserService)
    {
        _ocrDocumentService = ocrDocumentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// POST /api/ocr/extract - Seller or Admin only. Accepts a PDF or
    /// image (JPG/JPEG/PNG/TIFF) land deed document and returns the raw
    /// OCR text, page count, confidence, and the placeholder field
    /// extraction.
    ///
    /// Bound as a single <see cref="OcrUploadRequest"/> model, the same
    /// Swashbuckle-safe upload pattern <c>PropertyController.AddImage</c>
    /// established in Module 4 (see that DTO's doc comment).
    /// </summary>
    [HttpPost("extract")]
    [Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> Extract([FromForm] OcrUploadRequest request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }

        await using var stream = file.OpenReadStream();

        var result = await _ocrDocumentService.ExtractAsync(
            file.FileName, file.ContentType, stream, callerId, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
