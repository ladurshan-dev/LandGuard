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
    /// package</b> - either a project-local folder populated with the
    /// language file(s) matching <see cref="Language"/> from the
    /// tessdata_fast/tessdata repository, or a system Tesseract
    /// installation's own tessdata directory (e.g.
    /// "C:\Program Files\Tesseract-OCR\tessdata" - see this project's
    /// appsettings.Development.json for the Windows development-machine
    /// override). A missing/empty folder makes every OCR call fail at the
    /// time of the first request, not at application startup
    /// (TesseractEngine is constructed lazily per call - see
    /// TesseractOcrService).
    ///
    /// Resolved by <c>TesseractOcrService.ResolveTessDataPath</c>: an
    /// absolute value here (like the Windows path above) is used exactly
    /// as configured; the "tessdata" default below is resolved against
    /// the API's own content root, not the OS process's current working
    /// directory - see that method's doc comment.
    /// </summary>
    public string TessDataPath { get; set; } = "tessdata";

    /// <summary>Tesseract language code(s), e.g. "eng" or "eng+sin" for English+Sinhala.</summary>
    public string Language { get; set; } = "eng";
}
