using FluentValidation;
using LandGuard.Application.DTOs.Auth.Validators;

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

        // Every field is still optional here (omitting it leaves the
        // existing value unchanged, matching usp_Property_Update's
        // ISNULL-coalesce pattern) - but if the caller DOES supply one of
        // the 4 mandatory ownership/deed fields on an edit, it may not be
        // blanked out, exactly like Title/Location above.
        RuleFor(x => x.DeedReference)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.DeedReferenceMaxLength)
            .When(x => x.DeedReference is not null);

        RuleFor(x => x.OwnerName)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.OwnerNameMaxLength)
            .When(x => x.OwnerName is not null);

        RuleFor(x => x.OwnerNic)
            .NotEmpty()
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage)
            .When(x => x.OwnerNic is not null);

        RuleFor(x => x.OwnerAddress)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.OwnerAddressMaxLength)
            .When(x => x.OwnerAddress is not null);
    }
}
