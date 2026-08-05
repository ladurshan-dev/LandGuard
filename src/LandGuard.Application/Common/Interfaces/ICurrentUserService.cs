namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Exposes identity of the currently authenticated caller (extracted from
/// JWT claims) to the Application layer. Services such as PropertyService
/// need to know "which Seller uploaded this" or "is the caller an Admin"
/// without taking a dependency on HttpContext/ClaimsPrincipal directly -
/// that dependency lives only in Infrastructure's CurrentUserService
/// implementation, keeping Application layer unit-testable with a fake.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Id of the authenticated user, or null if unauthenticated.</summary>
    int? UserId { get; }

    string? Email { get; }

    /// <summary>Raw role claim value (Buyer / Seller / Administrator).</summary>
    string? Role { get; }

    bool IsAuthenticated { get; }
}
