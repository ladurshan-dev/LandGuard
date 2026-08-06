namespace LandGuard.Application.Common.Models;

/// <summary>
/// Matches the result set of <c>usp_User_Login</c> exactly - the only
/// stored procedure in the schema that returns <c>PasswordHash</c>, since
/// verifying it (BCrypt comparison) can only happen in C#, not T-SQL.
///
/// Internal to the Auth flow: <see cref="Services.AuthService"/> uses this
/// only to verify a password and then immediately discards it in favour
/// of the hash-free <see cref="UserProfile"/> for anything that leaves the
/// service (the JWT claims, the API response). No controller or DTO
/// should ever reference this type directly.
/// </summary>
public class UserCredential
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    /// <summary>Raw database value - "Buyer" | "Seller" | "Admin".</summary>
    public string Role { get; set; } = null!;

    public string? Nic { get; set; }

    public string? Phone { get; set; }

    public bool NicVerified { get; set; }

    public bool IsActive { get; set; }
}
