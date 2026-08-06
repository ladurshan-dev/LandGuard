using FluentValidation;

namespace LandGuard.Application.DTOs.Auth.Validators;

public class BuyerRegisterRequestValidator : AbstractValidator<BuyerRegisterRequest>
{
    public BuyerRegisterRequestValidator()
    {
        RuleFor(x => x.Name)
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

        // Optional for a Buyer - only validated when supplied.
        RuleFor(x => x.Nic)
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.Nic));

        RuleFor(x => x.Phone)
            .MaximumLength(20); // dbo.Users.Phone VARCHAR(20)
    }
}
