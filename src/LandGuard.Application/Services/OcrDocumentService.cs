using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Ocr;
using LandGuard.Application.DTOs.Ocr.Validators;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates one OCR extraction request: validate the upload, save the
/// original (via <see cref="IFileStorageService"/>, unchanged from
/// Module 4 beyond its additive <c>SaveDocumentAsync</c> method), run OCR
/// (<see cref="IOcrService"/>, Infrastructure/Tesseract), and apply the
/// placeholder field heuristics (<see cref="DocumentFieldExtractor"/>).
/// No SQL, no HTTP, no fraud logic anywhere in this class.
///
/// <b>How Module 5C is expected to consume this:</b> today,
/// <see cref="ExtractAsync"/> returns its result directly to the caller
/// and persists nothing to LandGuardDB - Module 5B was explicitly told not
/// to make database changes unless required, and none were. The
/// <see cref="OcrResultResponse"/> (raw text, per-page confidence, the 10
/// placeholder <see cref="ExtractedField"/> values, and the saved
/// document's <c>DocumentReference</c>) is shaped so Module 5C's fraud
/// comparison layer can take it as input directly - either passed straight
/// through from the API response by whatever client calls
/// <c>/api/ocr/extract</c> and then a Module 5C endpoint, or, if Module 5C
/// prefers server-side persistence, by adding a new
/// <c>usp_Fraud_SaveExtractedDocument</c>-style procedure at that point to
/// store <see cref="ExtractedField"/> values keyed to a property - a
/// decision deliberately left for Module 5C rather than guessed here.
/// </summary>
public class OcrDocumentService : IOcrDocumentService
{
    private readonly IOcrService _ocrService;
    private readonly IFileStorageService _fileStorageService;

    public OcrDocumentService(IOcrService ocrService, IFileStorageService fileStorageService)
    {
        _ocrService = ocrService;
        _fileStorageService = fileStorageService;
    }

    public async Task<Result<OcrResultResponse>> ExtractAsync(
        string fileName, string contentType, Stream content, int uploadedByUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || !OcrValidationRules.AllowedDocumentContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<OcrResultResponse>.Failure(
                $"Unsupported file type '{contentType}'. Allowed: PDF, JPG, JPEG, PNG, TIFF.");
        }

        // Buffered once so the same bytes can be both persisted
        // (IFileStorageService) and OCR'd (IOcrService) without relying on
        // a single Stream instance being safely re-readable twice.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return Result<OcrResultResponse>.Failure("No file was uploaded.");
        }

        if (buffer.Length > OcrValidationRules.MaxDocumentSizeBytes)
        {
            return Result<OcrResultResponse>.Failure(
                $"File exceeds the maximum allowed size of {OcrValidationRules.MaxDocumentSizeBytes / (1024 * 1024)} MB.");
        }

        var bytes = buffer.ToArray();

        var stored = await _fileStorageService.SaveDocumentAsync(
            uploadedByUserId, fileName, contentType, new MemoryStream(bytes), cancellationToken);

        var extraction = await _ocrService.ExtractTextAsync(new MemoryStream(bytes), contentType, cancellationToken);

        var fields = DocumentFieldExtractor.Extract(extraction.RawText);

        var response = new OcrResultResponse
        {
            FileName = fileName,
            DocumentReference = stored.StorageReference,
            Text = new DocumentTextResponse
            {
                RawText = extraction.RawText,
                PageCount = extraction.PageCount,
                Confidence = extraction.Confidence
            },
            Fields = fields
        };

        return Result<OcrResultResponse>.Success(response);
    }
}
