using FluentValidation;
using LandGuard.Application.DTOs.Auth.Validators;

namespace LandGuard.Application.DTOs.Property.Validators;

public class CreatePropertyRequestValidator : AbstractValidator<CreatePropertyRequest>
{
    public CreatePropertyRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.TitleMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(PropertyValidationRules.DescriptionMaxLength);

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.LocationMaxLength);

        RuleFor(x => x.District)
            .MaximumLength(PropertyValidationRules.DistrictMaxLength);

        // General geo sanity only - CK_Property_Status/engine-specific bounds
        // (Sri Lanka's bounding box) are a fraud signal, not a validation
        // error, so out-of-country coordinates are accepted here and left
        // for fraud rule 6 (Location Validation) to flag.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.Size)
            .GreaterThan(0); // CK_Property_Size

        RuleFor(x => x.Price)
            .GreaterThan(0); // CK_Property_Price

        // Owner Name / Owner NIC / Owner Address / Deed Number requirement:
        // all four mandatory for every new listing - a Property with no
        // deed, or a deed with no stated owner, must never be creatable.
        RuleFor(x => x.DeedReference)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.DeedReferenceMaxLength);

        RuleFor(x => x.OwnerName)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.OwnerNameMaxLength);

        RuleFor(x => x.OwnerNic)
            .NotEmpty()
            .Matches(AuthValidationRules.NicPattern)
            .WithMessage(AuthValidationRules.NicErrorMessage);

        RuleFor(x => x.OwnerAddress)
            .NotEmpty()
            .MaximumLength(PropertyValidationRules.OwnerAddressMaxLength);
    }
}
