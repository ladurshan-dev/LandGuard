using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Registration, login and account self-service. Every action here is a
/// thin translation from HTTP to <see cref="IAuthService"/> and back - all
/// business logic (password hashing, token issuance, the account-
/// enumeration-safe login message) lives in AuthService, not here.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(IAuthService authService, ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }

    /// <summary>POST /api/auth/register/buyer - anonymous, creates a Buyer account and logs it in immediately.</summary>
    [HttpPost("register/buyer")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterBuyer([FromBody] BuyerRegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterBuyerAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>POST /api/auth/register/seller - anonymous, creates a Seller account and logs it in immediately. NIC is required.</summary>
    [HttpPost("register/seller")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterSeller([FromBody] SellerRegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterSellerAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/auth/register - anonymous, self-registration for Buyer or
    /// Seller (never Admin - see RegisterRequestValidator's Role whitelist
    /// and AuthService.RegisterAsync's doc comment). Creates the account and
    /// logs it in immediately, same as register/buyer and register/seller.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>POST /api/auth/login - anonymous. Returns the same generic error for an unknown email or a wrong password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : Unauthorized(new { errors = result.Errors });
    }

    /// <summary>GET /api/auth/me - requires a valid JWT (any role). Returns the caller's own profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
                     ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>POST /api/auth/change-password - requires a valid JWT (any role). Target user always comes from the token, never the body.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
                     ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _authService.ChangePasswordAsync(userId, request, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/auth/identity/reverify - Seller Government Identity
    /// Verification requirement. Seller-only; the target account always
    /// comes from the caller's own JWT, never a request body UserID.
    /// Allowed for a Pending or a Failed Seller - see
    /// IAuthService.ReverifyIdentityAsync's own doc comment.
    /// </summary>
    [HttpPost("identity/reverify")]
    [Authorize(Policy = AuthorizationPolicies.RequireSeller)]
    public async Task<IActionResult> ReverifyIdentity(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
                     ?? throw new UnauthorizedAccessException("No authenticated user on the current request.");

        var result = await _authService.ReverifyIdentityAsync(userId, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
