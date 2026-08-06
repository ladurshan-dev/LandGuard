namespace LandGuard.Application.DTOs.Auth;

/// <summary>POST /api/auth/register/seller. NIC is required (CK_Users_Seller_NIC, FR02).</summary>
public class SellerRegisterRequest
{
    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Nic { get; set; } = null!;

    public string? Phone { get; set; }
}
