using LandGuard.Application.Common.Models;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over JWT issuance. Application depends on this, not on
/// System.IdentityModel.Tokens.Jwt directly - the same Dependency
/// Inversion pattern as every other Infrastructure concern in this
/// solution. Takes the strongly-typed <see cref="UserRole"/> rather than
/// a raw string; the implementation (Infrastructure) is responsible for
/// converting it to the database's role string via
/// <c>UserRoleExtensions.ToDbValue</c> when it writes the role claim, so
/// Application code never has to know that "Administrator" is spelled
/// "Admin" on the wire.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Issues a signed access token carrying the claims
    /// <see cref="ICurrentUserService"/> expects to read back later
    /// (NameIdentifier, Email, Role) plus the user's display name.
    /// </summary>
    AccessToken GenerateToken(int userId, string email, string name, UserRole role);
}
