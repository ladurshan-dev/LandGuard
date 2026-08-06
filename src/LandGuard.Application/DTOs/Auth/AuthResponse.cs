using LandGuard.Application.Common.Models;

namespace LandGuard.Application.DTOs.Auth;

/// <summary>
/// Returned by Register (Buyer/Seller) and Login alike - registering logs
/// the new account straight in, so the frontend never has to make a
/// second round trip just to obtain a token after signing up.
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public UserProfile User { get; set; } = null!;
}
