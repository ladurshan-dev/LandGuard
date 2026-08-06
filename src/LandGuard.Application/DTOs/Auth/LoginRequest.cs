namespace LandGuard.Application.DTOs.Auth;

/// <summary>POST /api/auth/login.</summary>
public class LoginRequest
{
    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;
}
