using FluentValidation;

namespace LandGuard.Application.DTOs.Admin.Validators;

public class ApprovePropertyRequestValidator : AbstractValidator<ApprovePropertyRequest>
{
    public ApprovePropertyRequestValidator()
    {
        // Matches usp_Admin_ApproveProperty's @Remarks NVARCHAR(500).
        RuleFor(x => x.Remarks)
            .MaximumLength(500);
    }
}
