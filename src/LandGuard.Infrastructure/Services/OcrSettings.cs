namespace LandGuard.Infrastructure.Services;

/// <summary>
/// Strongly-typed binding of the "Ocr" configuration section, bound via
/// <c>services.Configure&lt;OcrSettings&gt;</c> and used by
/// <see cref="TesseractOcrService"/>.
/// </summary>
public class OcrSettings
{
    /// <summary>
    /// Folder containing Tesseract's trained-data files (e.g.
    /// <c>eng.traineddata</c>). <b>Not shipped by the Tesseract NuGet
    /// package</b> - download the language file(s) matching
    /// <see cref="Language"/> from the tessdata_fast/tessdata repository
    /// and place them here before running the API; a missing/empty folder
    /// makes every OCR call fail at startup of the first request, not at
    /// application startup (TesseractEngine is constructed lazily per
    /// call - see TesseractOcrService).
    /// </summary>
    public string TessDataPath { get; set; } = "tessdata";

    /// <summary>Tesseract language code(s), e.g. "eng" or "eng+sin" for English+Sinhala.</summary>
    public string Language { get; set; } = "eng";
}
