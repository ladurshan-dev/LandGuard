namespace LandGuard.Application.DTOs.Ocr.Validators;

/// <summary>
/// Shared validation constants for OCR document uploads, mirroring the
/// role <c>PropertyValidationRules</c> plays for property image uploads
/// (Module 4) - checked directly in <c>OcrDocumentService</c> rather than
/// via a FluentValidation validator, since there is no multi-field DTO
/// here to validate, just a single file's metadata (same reasoning
/// <c>PropertyService.AddImageAsync</c> already established).
/// </summary>
internal static class OcrValidationRules
{
    /// <summary>Larger than a property photo's cap - deed scans/PDFs run bigger than a single marketing photo.</summary>
    public const long MaxDocumentSizeBytes = 15 * 1024 * 1024;

    public static readonly string[] AllowedDocumentContentTypes =
        { "application/pdf", "image/jpeg", "image/png", "image/tiff" };
}
