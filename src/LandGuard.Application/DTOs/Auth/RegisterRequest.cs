namespace LandGuard.Application.DTOs.Auth;

/// <summary>
/// POST /api/auth/register - the single public self-registration endpoint
/// used by the frontend's /register page. Unlike
/// <see cref="BuyerRegisterRequest"/>/<see cref="SellerRegisterRequest"/>
/// (which stay as they were, and are still reachable at their own
/// register/buyer and register/seller routes), the role here is not
/// implied by which endpoint was called - it is a field in the body, so it
/// must be validated server-side rather than trusted. See
/// <c>RegisterRequestValidator</c>'s whitelist rule and
/// <c>AuthService.RegisterAsync</c>'s doc comment for exactly how a
/// caller-supplied "Admin" (or any other value) is rejected before it can
/// ever reach <c>usp_User_Register</c>.
/// </summary>
public class RegisterRequest
{
    /// <summary>Maps to dbo.Users.Name - the same column BuyerRegisterRequest.Name/SellerRegisterRequest.Name write.</summary>
    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    /// <summary>Never sent to the database - checked only by RegisterRequestValidator against Password.</summary>
    public string ConfirmPassword { get; set; } = null!;

    /// <summary>Must be exactly "Buyer" or "Seller" - see RegisterRequestValidator. Never "Admin": there is no public path to create an Admin account.</summary>
    public string Role { get; set; } = null!;

    /// <summary>Required when Role is "Seller" (CK_Users_Seller_NIC, FR02); optional and format-validated-if-supplied when Role is "Buyer".</summary>
    public string? Nic { get; set; }

    public string? Phone { get; set; }
}
