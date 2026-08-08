using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IOcrService" />
/// <remarks>
/// Local-only OCR via the <c>Tesseract</c> NuGet package (a .NET wrapper
/// around the native Tesseract/Leptonica engine - the package bundles
/// win-x64/win-x86 native binaries, matching this project's Windows/SQL
/// Server Express deployment target; a Linux/Mac host needs
/// libtesseract/libleptonica installed via its own package manager).
/// PDFs are rasterized page-by-page with <c>PDFtoImage</c> (a PDFium
/// wrapper) into PNG bytes via SkiaSharp before being handed to Tesseract,
/// since Leptonica reads JPEG/PNG/TIFF directly but not PDF.
///
/// <see cref="TesseractEngine"/> is not safe to reuse across concurrent
/// calls, and its <c>Process</c> call is synchronous native code with no
/// async overload - a new engine is constructed per extraction and the
/// whole OCR pass is offloaded via <see cref="Task.Run(Func{object?},CancellationToken)"/>
/// so it never blocks a request thread for however long OCR takes.
///
/// <see cref="OcrSettings.TessDataPath"/> is resolved via
/// <see cref="ResolveTessDataPath"/> exactly the way
/// <c>LocalFileStorageService</c> already resolves
/// <c>FileStorageSettings.RootPath</c>/<c>DocumentsRootPath</c>: rooted
/// (absolute) as configured, or relative to <see cref="IWebHostEnvironment.ContentRootPath"/>
/// otherwise - never passed to <see cref="TesseractEngine"/> as a bare
/// relative string, which previously left it resolved against the OS
/// process's current working directory instead (see
/// <see cref="ResolveTessDataPath"/>'s own doc comment for why that was
/// the root cause of a "tessdata/eng.traineddata" load failure on a
/// machine using a system-installed Tesseract).
/// </remarks>
public class TesseractOcrService : IOcrService
{
    private readonly OcrSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TesseractOcrService> _logger;

    public TesseractOcrService(IOptions<OcrSettings> options, IWebHostEnvironment environment, ILogger<TesseractOcrService> logger)
    {
        _settings = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<OcrExtractionResult> ExtractTextAsync(
        Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        return await Task.Run(() => RunOcr(bytes, contentType), cancellationToken);
    }

    private OcrExtractionResult RunOcr(byte[] bytes, string contentType)
    {
        var pageImages = IsPdf(contentType) ? RasterizePdf(bytes) : new[] { bytes };

        var tessDataPath = ResolveTessDataPath();

        _logger.LogInformation(
            "Running Tesseract OCR ({Language}) over {PageCount} page(s), content type {ContentType}, tessdata path '{TessDataPath}'",
            _settings.Language, pageImages.Count, contentType, tessDataPath);

        using var engine = new TesseractEngine(tessDataPath, _settings.Language, EngineMode.Default);

        var pageTexts = new List<string>();
        var confidences = new List<float>();

        foreach (var pageBytes in pageImages)
        {
            using var pix = Pix.LoadFromMemory(pageBytes);
            using var page = engine.Process(pix);

            pageTexts.Add(page.GetText()?.Trim() ?? string.Empty);
            confidences.Add(page.GetMeanConfidence());
        }

        return new OcrExtractionResult
        {
            RawText = string.Join("\n\n----- Page Break -----\n\n", pageTexts),
            Pages = pageTexts,
            PageCount = pageTexts.Count,
            // Tesseract reports confidence as 0-1; the rest of this
            // solution's DTOs use a 0-100 scale (see RiskSummaryResponse),
            // so this is converted here, once, at the source.
            Confidence = confidences.Count > 0 ? (decimal)(confidences.Average() * 100) : null
        };
    }

    /// <summary>
    /// Resolves <see cref="OcrSettings.TessDataPath"/> into the actual
    /// directory handed to <see cref="TesseractEngine"/>'s constructor.
    /// Previously that raw config value was passed straight through -
    /// harmless for an absolute path, but for the "tessdata" default it
    /// meant Tesseract/Leptonica resolved it relative to the OS process's
    /// current working directory, not this project's own folder. On a
    /// machine where the process starts from a different working
    /// directory (IIS Express, a Windows service, `dotnet exec` from
    /// elsewhere, ...), that relative lookup misses entirely - exactly the
    /// "Error opening data file tessdata/eng.traineddata... TESSDATA_PREFIX"
    /// failure this method fixes. Rooted values (e.g. a system-installed
    /// Tesseract's own tessdata folder, such as
    /// "C:\Program Files\Tesseract-OCR\tessdata" on this Windows
    /// development machine, configured via appsettings.Development.json)
    /// are returned unchanged; a relative value resolves against
    /// <see cref="IWebHostEnvironment.ContentRootPath"/> instead - the
    /// exact same rule <c>LocalFileStorageService</c> already applies to
    /// <c>FileStorageSettings.RootPath</c>/<c>DocumentsRootPath</c>, reused
    /// here rather than inventing a second convention.
    /// </summary>
    private string ResolveTessDataPath() =>
        Path.IsPathRooted(_settings.TessDataPath)
            ? _settings.TessDataPath
            : Path.Combine(_environment.ContentRootPath, _settings.TessDataPath);

    private static bool IsPdf(string contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<byte[]> RasterizePdf(byte[] pdfBytes)
    {
        var pages = new List<byte[]>();

        foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfBytes))
        {
            using (bitmap)
            {
                using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                pages.Add(encoded.ToArray());
            }
        }

        return pages;
    }
}
