using FluentValidation;

namespace LandGuard.Application.DTOs.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        // No strength rule here on purpose - strength is enforced at
        // registration/change-password time, not re-checked at login
        // (an existing account may predate a rule, and login should only
        // ever say "this doesn't match", never "this doesn't meet policy").
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
