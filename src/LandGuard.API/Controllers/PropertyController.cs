using LandGuard.API.Authorization;
using LandGuard.API.Models;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.Property;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Property CRUD, image upload and search - Module 4. Every action is a
/// thin translation from HTTP to <see cref="IPropertyService"/> and back;
/// all business logic (ownership rules, geocoding, fraud-engine
/// re-triggering) lives in PropertyService, not here, the same split
/// AuthController established in Module 3.
///
/// Ownership for Update/Delete is not re-checked in this controller - it
/// is enforced by the stored procedures themselves (see
/// IPropertyStoredProcedures' doc comments), so both endpoints only
/// require the caller to be authenticated and let the service/procedure
/// layer decide. AddImage and GetBySeller have no database-side ownership
/// check of their own, so PropertyService enforces "owner or Admin" for
/// those directly.
/// </summary>
[ApiController]
[Route("api/properties")]
public class PropertyController : ControllerBase
{
    private readonly IPropertyService _propertyService;
    private readonly ICurrentUserService _currentUserService;

    public PropertyController(IPropertyService propertyService, ICurrentUserService currentUserService)
    {
        _propertyService = propertyService;
        _currentUserService = currentUserService;
    }

    /// <summary>GET /api/properties - public search over published listings only (FR10).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] PropertySearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _propertyService.SearchAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/properties/{id} - anonymous-accessible, but PropertyService
    /// only reveals a non-Approved listing to its owner or an Admin.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _propertyService.GetByIdAsync(
            id, _currentUserService.UserId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>GET /api/properties/seller/{sellerId} - the seller dashboard grid (FR08). Only that seller or an Admin may view it.</summary>
    [HttpGet("seller/{sellerId:int}")]
    [Authorize]
    public async Task<IActionResult> GetBySeller(int sellerId, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _propertyService.GetBySellerAsync(sellerId, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : StatusCode(StatusCodes.Status403Forbidden, new { errors = result.Errors });
    }

    /// <summary>POST /api/properties - Seller only. SellerId always comes from the caller's JWT, never the request body.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireSeller)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _propertyService.CreateAsync(request, sellerId, cancellationToken);

        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.PropertyId }, result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/properties/{id}/images - owner or Admin. Re-runs the fraud
    /// engine after attaching the photo so Duplicate Image/Missing
    /// Information reflect the listing's true current state.
    ///
    /// Bound as a single <see cref="UploadPropertyImageRequest"/> model
    /// rather than separate <c>[FromForm] IFormFile</c> +
    /// <c>[FromForm] bool</c> parameters - Swashbuckle's SwaggerGen cannot
    /// describe the latter shape (see UploadPropertyImageRequest's doc
    /// comment). The underlying upload handling is unchanged.
    /// </summary>
    [HttpPost("{id:int}/images")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> AddImage(
        int id, [FromForm] UploadPropertyImageRequest request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }

        await using var stream = file.OpenReadStream();

        var result = await _propertyService.AddImageAsync(
            id, file.FileName, file.ContentType, stream, request.IsPrimary, callerId, _currentUserService.Role, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>PUT /api/properties/{id} - Seller only; ownership is enforced by usp_Property_Update itself. Resets Status to Pending and re-runs the engine.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSeller)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyRequest request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _propertyService.UpdateAsync(id, request, sellerId, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>DELETE /api/properties/{id} - owner or Admin, enforced by usp_Property_Delete itself.</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _propertyService.DeleteAsync(id, callerId, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/properties/{id}/withdraw - Seller only (Phase F, Property
    /// Withdrawal / Soft Delete). This is the Seller-facing replacement for
    /// "Delete": it sets Status to "Withdrawn" instead of physically
    /// deleting the row, so DeedVerification/FraudCheck/RiskReport/
    /// AdminAction/Notification history is fully preserved. Ownership and
    /// the allowed source states (Pending/Approved only) are enforced by
    /// usp_Property_Withdraw itself, exactly like Update above. SellerId
    /// always comes from the caller's JWT, never the request body.
    /// DELETE /api/properties/{id} above is unchanged and remains an
    /// Admin-only hard-delete/cleanup path.
    /// </summary>
    [HttpPost("{id:int}/withdraw")]
    [Authorize(Policy = AuthorizationPolicies.RequireSeller)]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        var sellerId = _currentUserService.UserId
                       ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _propertyService.WithdrawAsync(id, sellerId, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
