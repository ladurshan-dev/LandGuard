namespace LandGuard.API.Models;

/// <summary>
/// Multipart form-data binding model for <c>POST /api/ocr/extract</c>.
/// Follows exactly the pattern <c>UploadPropertyImageRequest</c>
/// established in Module 4: a single class bound with one
/// <c>[FromForm]</c> on the action, rather than a bare
/// <c>[FromForm] IFormFile</c> action parameter - Swashbuckle's SwaggerGen
/// throws a <c>SwaggerGeneratorException</c> ("[FromForm] attribute used
/// with IFormFile") when an action mixes an <see cref="IFormFile"/>
/// parameter with other independently-bound <c>[FromForm]</c> parameters,
/// and wrapping the upload in one model is the shape it can always
/// describe, whether or not more fields are added here later.
/// </summary>
public class OcrUploadRequest
{
    public IFormFile File { get; set; } = null!;
}
