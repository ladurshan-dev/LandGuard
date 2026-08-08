using System.Security.Cryptography;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IFileStorageService" />
public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;
    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IOptions<FileStorageSettings> options, IWebHostEnvironment environment)
    {
        _settings = options.Value;
        _environment = environment;
    }

    public async Task<StoredImageFile> SaveImageAsync(
        int propertyId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        if (!_settings.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported image type '{contentType}'. Allowed: {string.Join(", ", _settings.AllowedContentTypes)}.",
                nameof(contentType));
        }

        var rootPath = Path.IsPathRooted(_settings.RootPath)
            ? _settings.RootPath
            : Path.Combine(_environment.ContentRootPath, _settings.RootPath);

        var (storedFileName, hash) = await WriteFileAndHashAsync(
            rootPath, propertyId.ToString(), fileName, content, cancellationToken);

        var url = $"{_settings.PublicBaseUrl.TrimEnd('/')}/{propertyId}/{storedFileName}";

        return new StoredImageFile(url, hash);
    }

    public async Task<StoredDocumentFile> SaveDocumentAsync(
        int uploadedByUserId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        if (!_settings.AllowedDocumentContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported document type '{contentType}'. Allowed: {string.Join(", ", _settings.AllowedDocumentContentTypes)}.",
                nameof(contentType));
        }

        // Deliberately resolved against DocumentsRootPath, not RootPath -
        // outside wwwroot, so nothing here is reachable via
        // app.UseStaticFiles() (see FileStorageSettings.DocumentsRootPath's
        // doc comment).
        var rootPath = Path.IsPathRooted(_settings.DocumentsRootPath)
            ? _settings.DocumentsRootPath
            : Path.Combine(_environment.ContentRootPath, _settings.DocumentsRootPath);

        var (storedFileName, hash) = await WriteFileAndHashAsync(
            rootPath, uploadedByUserId.ToString(), fileName, content, cancellationToken);

        var storageReference = $"documents/{uploadedByUserId}/{storedFileName}";

        return new StoredDocumentFile(storageReference, hash);
    }

    public Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Task.CompletedTask;
        }

        var rootPath = Path.IsPathRooted(_settings.RootPath)
            ? _settings.RootPath
            : Path.Combine(_environment.ContentRootPath, _settings.RootPath);
        var fullRootPath = Path.GetFullPath(rootPath);

        // ImageURL is always "{PublicBaseUrl}/..." - see SaveImageAsync
        // above, which is the only thing that ever produces it (both the
        // {propertyId}/{guid} shape a real upload writes, and the older
        // flat {filename} shape some seeded rows use - PublicBaseUrl is
        // the one prefix both share). Anything not starting with that
        // prefix isn't a URL this service issued, so there's nothing safe
        // to resolve to a physical path.
        var publicBaseUrl = _settings.PublicBaseUrl.TrimEnd('/');
        if (!imageUrl.StartsWith(publicBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relativePath = imageUrl[publicBaseUrl.Length..].TrimStart('/');

        // Reject anything that could escape RootPath (".." traversal, an
        // empty segment, etc.) before ever touching the filesystem - a
        // malformed/tampered ImageURL must never translate into deleting
        // something outside the configured upload directory.
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == ".." || segment == "."))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.GetFullPath(Path.Combine(fullRootPath, Path.Combine(segments)));

        // Final guard: the fully-resolved absolute path must still be
        // inside RootPath - defence in depth beyond the ".." segment
        // check above (also catches a rooted/drive-letter segment
        // smuggled into the stored URL).
        if (!fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Locked file, already-missing file raced out from under us,
            // or some other filesystem hiccup - the PropertyImage row is
            // the source of truth for "does this image exist", so a
            // failed physical delete must not fail the whole operation.
        }
        catch (UnauthorizedAccessException)
        {
            // Filesystem permission issue - same reasoning as above.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Shared by <see cref="SaveImageAsync"/> and <see cref="SaveDocumentAsync"/>:
    /// writes the upload to <paramref name="rootPath"/>/<paramref name="folderSegment"/>
    /// under a generated file name and SHA-256 hashes it in the same pass
    /// via CryptoStream, so callers never read the upload twice. The
    /// generated name (not the caller-supplied one) is what's actually
    /// used on disk - never trust a client-provided filename for a path
    /// segment; the original extension is kept only for a friendlier
    /// listing.
    /// </summary>
    private static async Task<(string StoredFileName, string Sha256Hash)> WriteFileAndHashAsync(
        string rootPath, string folderSegment, string fileName, Stream content, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(rootPath, folderSegment);
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, storedFileName);

        using var sha256 = SHA256.Create();

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        await using (var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write, leaveOpen: true))
        {
            await content.CopyToAsync(hashingStream, cancellationToken);
        }

        var hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

        return (storedFileName, hash);
    }
}
