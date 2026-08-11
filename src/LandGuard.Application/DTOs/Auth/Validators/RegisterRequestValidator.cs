using FluentValidation;

namespace LandGuard.Application.DTOs.Auth.Validators;

/// <summary>
/// Validates POST /api/auth/register's body before AuthService.RegisterAsync
/// dispatches to the existing RegisterBuyerAsync/RegisterSellerAsync (which
/// re-validate Name/Email/Password/Nic/Phone via
/// BuyerRegisterRequestValidator/SellerRegisterRequestValidator - that is
/// intentional layered validation, not duplicated logic, since this class
/// only owns the two rules unique to the unified endpoint: Role's
/// Buyer/Seller whitelist and ConfirmPassword.
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(150); // dbo.Users.Name NVARCHAR(150)

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150); // dbo.Users.Email NVARCHAR(150)

        RuleFor(x => x.Password)
            .NotEmpty()
            .Matches(AuthValidationRules.PasswordPattern)
            .WithMessage(AuthValidationRules.PasswordErrorMessage);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm Password is required.")
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");

        // The one rule standing between a client-supplied Role and
        // usp_User_Register - only "Buyer" and "Seller" pass; "Admin" (or
        // any other string) fails here and never reaches AuthService's
        // dispatch switch at all.
        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role => role is "Buyer" or "Seller")
            .WithMessage("Role must be Buyer or Seller.");

        // Required for a Seller (FR02 / CK_Users_Seller_NIC) - same message
        // SellerRegisterRequestValidator uses, so the two register paths
        // never disagree on wording.
        RuleFor(x => x.Nic)
            .NotEmpty()
            .WithMessage("A Sri Lankan NIC is required to register as a seller.")
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage)
            .When(x => x.Role == "Seller");

        // Optional for a Buyer - only validated when supplied, matching
        // BuyerRegisterRequestValidator.
        RuleFor(x => x.Nic)
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage)
            .When(x => x.Role == "Buyer" && !string.IsNullOrWhiteSpace(x.Nic));

        RuleFor(x => x.Phone)
            .MaximumLength(20); // dbo.Users.Phone VARCHAR(20)
    }
}
