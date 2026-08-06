using FluentValidation;

namespace LandGuard.Application.DTOs.Property.Validators;

public class PropertySearchRequestValidator : AbstractValidator<PropertySearchRequest>
{
    public PropertySearchRequestValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200);

        RuleFor(x => x.District)
            .MaximumLength(PropertyValidationRules.DistrictMaxLength);

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x)
            .Must(x => x.MinPrice!.Value <= x.MaxPrice!.Value)
            .WithMessage("MinPrice must not be greater than MaxPrice.")
            .WithName("MinPrice")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.MinSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinSize.HasValue);

        RuleFor(x => x.MaxSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxSize.HasValue);

        RuleFor(x => x)
            .Must(x => x.MinSize!.Value <= x.MaxSize!.Value)
            .WithMessage("MinSize must not be greater than MaxSize.")
            .WithName("MinSize")
            .When(x => x.MinSize.HasValue && x.MaxSize.HasValue);

        RuleFor(x => x.RiskLevel)
            .Must(v => PropertyValidationRules.ValidRiskLevels.Contains(v))
            .WithMessage("RiskLevel must be one of: Low, Medium, High.")
            .When(x => x.RiskLevel is not null);

        RuleFor(x => x.SortBy)
            .Must(v => PropertyValidationRules.ValidSortOptions.Contains(v))
            .WithMessage("SortBy must be one of: Newest, PriceAsc, PriceDesc, RiskAsc.");

        // PageNumber/PageSize deliberately unvalidated - see PropertySearchRequest's doc comment.
    }
}
