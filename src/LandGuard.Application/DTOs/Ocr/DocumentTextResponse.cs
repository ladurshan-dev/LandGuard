namespace LandGuard.Application.DTOs.Ocr;

/// <summary>The raw-text portion of <see cref="OcrResultResponse"/> - exactly what Tesseract read, with no field parsing applied.</summary>
public class DocumentTextResponse
{
    /// <summary>Every page's text concatenated, separated by a page-break marker.</summary>
    public string RawText { get; set; } = null!;

    public int PageCount { get; set; }

    /// <summary>0-100, averaged across pages - null if unavailable.</summary>
    public decimal? Confidence { get; set; }
}
