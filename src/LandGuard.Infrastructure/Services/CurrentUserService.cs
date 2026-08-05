using System.Security.Claims;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace LandGuard.Infrastructure.Services;

/// <summary>
/// Reads the authenticated caller's identity from the JWT claims attached
/// to the current HTTP request by ASP.NET Core's authentication
/// middleware. Depends on IHttpContextAccessor (not HttpContext directly)
/// so this service can be constructed outside a request scope - e.g. from
/// a future background job that recomputes Seller History risk scores -
/// without throwing a null reference exception.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
