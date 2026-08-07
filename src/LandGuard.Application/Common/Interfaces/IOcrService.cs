using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Abstraction over running local OCR against a single uploaded document,
/// so Application code (<c>OcrDocumentService</c>) never references
/// Tesseract or any PDF-rasterization library directly - the same
/// Dependency Inversion pattern as every other external concern in this
/// solution (<see cref="IPasswordHasher"/>, <see cref="IGeocodingService"/>).
/// Implemented in Infrastructure by <c>TesseractOcrService</c>, running
/// entirely locally - no cloud OCR/vision API of any kind, per Module 5B's
/// explicit requirement.
///
/// Text extraction only: no field parsing (that's
/// <c>Services.DocumentFieldExtractor</c>, pure Application-layer regex
/// logic with no OCR dependency) and no fraud comparison of any kind -
/// that is explicitly out of scope for this module and left for Module 5C.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Runs OCR against <paramref name="content"/>. PDFs
    /// (<paramref name="contentType"/> "application/pdf") are rasterized
    /// page-by-page before OCR, since Tesseract/Leptonica read image
    /// formats (JPEG/PNG/TIFF) directly but not PDF.
    /// </summary>
    Task<OcrExtractionResult> ExtractTextAsync(
        Stream content, string contentType, CancellationToken cancellationToken = default);
}
