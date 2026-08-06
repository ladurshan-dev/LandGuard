namespace LandGuard.API.Models;

/// <summary>
/// Multipart form-data binding model for
/// <c>POST /api/properties/{id}/images</c>. Swashbuckle's SwaggerGen
/// throws <c>SwaggerGeneratorException</c> ("[FromForm] attribute used
/// with IFormFile") when an action mixes an <see cref="IFormFile"/>
/// parameter with other <c>[FromForm]</c> scalar parameters directly in
/// its signature - it only knows how to describe a single bound model for
/// a multipart request, not several independently-bound parameters.
/// Wrapping the upload in one class (bound with a single
/// <c>[FromForm]</c> on the action) is the supported shape; nothing about
/// how the file is read or validated changes - <c>PropertyController</c>
/// still passes the same <c>File</c>/<c>IsPrimary</c> values straight
/// through to <c>IPropertyService.AddImageAsync</c>.
/// </summary>
public class UploadPropertyImageRequest
{
    public IFormFile File { get; set; } = null!;

    public bool IsPrimary { get; set; }
}
