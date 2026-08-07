namespace LandGuard.Application.DTOs.Ocr;

/// <summary>
/// POST /api/ocr/extract's response - the raw OCR text plus the
/// placeholder field extraction, for whatever uploaded the document
/// (Module 5C, or a future review UI) to consume next. Contains no fraud
/// analysis or comparison of any kind - this module only extracts.
/// </summary>
public class OcrResultResponse
{
    /// <summary>The original uploaded file name, as supplied by the caller.</summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// A storage reference for the saved original (see
    /// <c>StoredDocumentFile.StorageReference</c>'s doc comment) - not yet
    /// a working public URL, since a deed document can contain personal
    /// identity information.
    /// </summary>
    public string DocumentReference { get; set; } = null!;

    public DocumentTextResponse Text { get; set; } = null!;

    public IReadOnlyList<ExtractedField> Fields { get; set; } = Array.Empty<ExtractedField>();
}
