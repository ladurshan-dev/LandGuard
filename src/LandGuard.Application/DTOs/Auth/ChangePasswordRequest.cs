namespace LandGuard.Application.DTOs.Auth;

/// <summary>
/// POST /api/auth/change-password. The target user comes from the caller's
/// JWT (ICurrentUserService), never from the request body - a user can
/// only ever change their own password through this endpoint.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;
}
