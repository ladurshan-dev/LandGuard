namespace LandGuard.Application.Common.Models;

/// <summary>
/// Safe-to-return user shape - deliberately excludes PasswordHash. Matches
/// the result set of both <c>usp_User_Register</c> and
/// <c>usp_User_GetById</c> exactly (they select the same nine columns),
/// so a single Dapper-mapped type serves both.
/// </summary>
public class UserProfile
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    /// <summary>Raw database value - "Buyer" | "Seller" | "Admin". Use UserRoleExtensions.FromDbValue to get the enum.</summary>
    public string Role { get; set; } = null!;

    public string? Nic { get; set; }

    public string? Phone { get; set; }

    public bool NicVerified { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
