using FluentValidation;

namespace LandGuard.Application.DTOs.Admin.Validators;

public class RejectPropertyRequestValidator : AbstractValidator<RejectPropertyRequest>
{
    public RejectPropertyRequestValidator()
    {
        // Matches usp_Admin_RejectProperty's @Remarks NVARCHAR(500) -
        // NotEmpty is an API-layer requirement, not a stored-procedure one
        // (see RejectPropertyRequest's own doc comment for why).
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
