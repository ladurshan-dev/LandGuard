using FluentValidation;

namespace LandGuard.Application.DTOs.Auth.Validators;

public class SellerRegisterRequestValidator : AbstractValidator<SellerRegisterRequest>
{
    public SellerRegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.Password)
            .NotEmpty()
            .Matches(AuthValidationRules.PasswordPattern)
            .WithMessage(AuthValidationRules.PasswordErrorMessage);

        // Required for a Seller (FR02 / CK_Users_Seller_NIC).
        RuleFor(x => x.Nic)
            .NotEmpty()
            .WithMessage("A Sri Lankan NIC is required to register as a seller.")
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage);

        RuleFor(x => x.Phone)
            .MaximumLength(20);
    }
}
