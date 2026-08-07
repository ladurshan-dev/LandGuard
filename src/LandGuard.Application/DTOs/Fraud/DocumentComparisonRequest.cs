using LandGuard.Application.DTOs.Ocr;

namespace LandGuard.Application.DTOs.Fraud;

/// <summary>
/// POST /api/fraud/compare/{propertyId}'s request body. Deliberately
/// shaped so a caller can pass Module 5B's own
/// POST /api/ocr/extract response straight through with no
/// transformation: <see cref="Fields"/> reuses
/// <c>LandGuard.Application.DTOs.Ocr.ExtractedField</c> directly (the
/// exact type <c>OcrResultResponse.Fields</c> already is), rather than a
/// near-duplicate "request" type. Module 5C does not re-run OCR - this is
/// the "already produced OCR results" the brief refers to.
/// </summary>
public class DocumentComparisonRequest
{
    /// <summary>Optional - Module 5B's OcrResultResponse.DocumentReference, kept only for traceability on the saved comparison row.</summary>
    public string? DocumentReference { get; set; }

    /// <summary>The OCR-extracted fields to compare against LandGuardDB - exactly OcrResultResponse.Fields from a prior POST /api/ocr/extract call. Must be non-empty.</summary>
    public IReadOnlyList<ExtractedField> Fields { get; set; } = Array.Empty<ExtractedField>();
}
