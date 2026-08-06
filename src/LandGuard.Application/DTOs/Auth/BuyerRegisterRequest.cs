namespace LandGuard.Application.DTOs.Auth;

/// <summary>POST /api/auth/register/buyer. NIC is optional for a Buyer (CK_Users_Seller_NIC only requires it for a Seller).</summary>
public class BuyerRegisterRequest
{
    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Nic { get; set; }

    public string? Phone { get; set; }
}
