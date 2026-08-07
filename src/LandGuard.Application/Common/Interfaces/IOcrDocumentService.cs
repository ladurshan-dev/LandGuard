using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Ocr;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for Module 5B (OCR Integration). OcrController
/// depends only on this interface, never on <c>OcrDocumentService</c>
/// directly or on <see cref="IOcrService"/>/<see cref="IFileStorageService"/>
/// - the same shape every other service in this solution uses.
///
/// Extraction only: validates the upload, saves it, runs OCR, and applies
/// the placeholder field heuristics. No fraud scoring, no fraud
/// comparison, no risk calculation - explicitly out of scope for this
/// module (see <c>Services.OcrDocumentService</c>'s doc comment for how
/// Module 5C is expected to consume the result instead).
/// </summary>
public interface IOcrDocumentService
{
    /// <summary>
    /// Validates, stores and OCRs one uploaded document. Returns a
    /// <see cref="Result{T}"/> failure for an unsupported file type, an
    /// empty upload, or one exceeding the configured size ceiling -
    /// exceptions are reserved for genuinely unexpected failures (e.g. a
    /// disk write error), the same split every other service in this
    /// solution uses.
    /// </summary>
    Task<Result<OcrResultResponse>> ExtractAsync(
        string fileName, string contentType, Stream content, int uploadedByUserId, CancellationToken cancellationToken = default);
}
