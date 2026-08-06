namespace LandGuard.Application.DTOs.Auth.Validators;

/// <summary>
/// Shared validation constants for the Auth DTO validators, so the
/// password-strength rule and the Sri Lankan NIC format regex are defined
/// once instead of being copied across BuyerRegisterRequestValidator,
/// SellerRegisterRequestValidator and ChangePasswordRequestValidator.
///
/// The NIC pattern mirrors <c>dbo.fn_IsValidNIC</c> exactly, so a request
/// that fails validation here would also have failed the database's own
/// CHECK constraint (<c>CK_Users_NIC_Format</c>) - this just surfaces that
/// same rule as a clear 400 response before a round trip to SQL, rather
/// than as an opaque RAISERROR-derived error.
/// </summary>
internal static class AuthValidationRules
{
    /// <summary>At least one lowercase letter, one uppercase letter, one digit, 8+ characters.</summary>
    public const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    public const string PasswordErrorMessage =
        "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, and a digit.";

    /// <summary>Old format: 9 digits + V/X. New format: 12 digits. Matches dbo.fn_IsValidNIC.</summary>
    public const string NicPattern = @"^([0-9]{9}[VvXx]|[0-9]{12})$";

    public const string NicErrorMessage = "Enter a valid Sri Lankan NIC (9 digits followed by V or X, or 12 digits).";
}
