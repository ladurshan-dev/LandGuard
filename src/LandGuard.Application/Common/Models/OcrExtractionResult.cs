namespace LandGuard.Application.Common.Models;

/// <summary>
/// Raw output of a local Tesseract OCR run over one document
/// (<see cref="Interfaces.IOcrService"/>) - text only, no field parsing
/// and no fraud logic. <c>Services.OcrDocumentService</c> is what turns
/// this into the per-field extraction the API returns.
/// </summary>
public class OcrExtractionResult
{
    /// <summary>Every page's text concatenated, separated by a page-break marker.</summary>
    public string RawText { get; set; } = null!;

    /// <summary>One entry per page, in order - a single-page image document has exactly one.</summary>
    public IReadOnlyList<string> Pages { get; set; } = Array.Empty<string>();

    public int PageCount { get; set; }

    /// <summary>0-100, averaged across pages - null if Tesseract reported no confidence value (e.g. a blank page).</summary>
    public decimal? Confidence { get; set; }
}
