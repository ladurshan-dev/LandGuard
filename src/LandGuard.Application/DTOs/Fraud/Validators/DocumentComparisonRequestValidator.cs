using FluentValidation;

namespace LandGuard.Application.DTOs.Fraud.Validators;

/// <summary>Enforces "OCR data exists" (one of Module 5C's stated validations) - a client that hasn't called POST /api/ocr/extract first, or that posts an empty Fields list, is rejected here rather than silently producing a comparison with nothing to compare.</summary>
public class DocumentComparisonRequestValidator : AbstractValidator<DocumentComparisonRequest>
{
    public DocumentComparisonRequestValidator()
    {
        RuleFor(x => x.Fields)
            .NotEmpty()
            .WithMessage("OCR data is required - call POST /api/ocr/extract first and pass its Fields here.");
    }
}
