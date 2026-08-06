using FluentValidation;

namespace LandGuard.Application.DTOs.Property.Validators;

public class UpdatePropertyRequestValidator : AbstractValidator<UpdatePropertyRequest>
{
    public UpdatePropertyRequestValidator()
    {
        // Every field is optional (ISNULL-patched by usp_Property_Update),
        // so each rule only fires when the caller actually supplied that
        // field - a null Title here means "leave it unchanged", not "clear
        // it", and must never fail NotEmpty.
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.TitleMaxLength)
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(PropertyValidationRules.DescriptionMaxLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.LocationMaxLength)
            .When(x => x.Location is not null);

        RuleFor(x => x.District)
            .MaximumLength(PropertyValidationRules.DistrictMaxLength)
            .When(x => x.District is not null);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.Size)
            .GreaterThan(0)
            .When(x => x.Size.HasValue);

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .When(x => x.Price.HasValue);

        RuleFor(x => x.DeedReference)
            .MaximumLength(PropertyValidationRules.DeedReferenceMaxLength)
            .When(x => x.DeedReference is not null);
    }
}
