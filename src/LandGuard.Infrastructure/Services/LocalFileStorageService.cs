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

    public async Task<StoredDocumentFile> SaveGovernmentDocumentAsync(
        string recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        if (!_settings.AllowedDocumentContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported document type '{contentType}'. Allowed: {string.Join(", ", _settings.AllowedDocumentContentTypes)}.",
                nameof(contentType));
        }

        // recordId becomes a directory segment below (government-registry/
        // {recordId}) - reject anything that could escape DocumentsRootPath
        // or collide with filesystem-reserved names before ever touching
        // disk, the same defensive posture DeleteImageAsync already applies
        // to a different caller-influenced path segment. Today's six
        // dummy RecordIds ("GR-000001" etc.) are always safe, but this
        // method must stay safe once a real government registry
        // implementation starts supplying RecordId values LandGuard did
        // not generate itself.
        if (string.IsNullOrWhiteSpace(recordId)
            || recordId is "." or ".."
            || recordId.IndexOfAny(new[] { '/', '\\' }) >= 0
            || recordId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{recordId}' is not a valid government registry record id.", nameof(recordId));
        }

        // Same root as SaveDocumentAsync (DocumentsRootPath, outside
        // wwwroot) - government documents are a second logical folder
        // under that one existing storage tree, not a second physical
        // storage root. See FileStorageSettings.DocumentsRootPath's doc
        // comment for why this must stay outside app.UseStaticFiles().
        var rootPath = Path.IsPathRooted(_settings.DocumentsRootPath)
            ? _settings.DocumentsRootPath
            : Path.Combine(_environment.ContentRootPath, _settings.DocumentsRootPath);

        // "government-registry" is a fixed literal, never a numeric
        // uploadedByUserId - the two document kinds can never collide on
        // disk, satisfying "completely separated from seller-uploaded
        // documents" structurally rather than by convention alone.
        var folderSegment = Path.Combine("government-registry", recordId);

        var (storedFileName, hash) = await WriteFileAndHashAsync(
            rootPath, folderSegment, fileName, content, cancellationToken);

        var storageReference = $"documents/government-registry/{recordId}/{storedFileName}";

        return new StoredDocumentFile(storageReference, hash);
    }

    public Task<Stream?> OpenDocumentAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
        {
            return Task.FromResult<Stream?>(null);
        }

        var rootPath = Path.IsPathRooted(_settings.DocumentsRootPath)
            ? _settings.DocumentsRootPath
            : Path.Combine(_environment.ContentRootPath, _settings.DocumentsRootPath);
        var fullRootPath = Path.GetFullPath(rootPath);

        // Every reference SaveDocumentAsync/SaveGovernmentDocumentAsync
        // ever returns starts with this fixed "documents/" prefix (see
        // both methods above) - deliberately generic over which of the two
        // issued it, rather than a second, government-specific read
        // method. Anything that doesn't start with it isn't a reference
        // this service issued, so there is nothing safe to open.
        const string prefix = "documents/";
        if (!storageReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<Stream?>(null);
        }

        var relativePath = storageReference[prefix.Length..];

        // Same defensive posture SaveGovernmentDocumentAsync and
        // DeleteImageAsync already apply to a caller-influenced path
        // segment: reject ".."/"." before ever touching the filesystem,
        // then re-verify the fully-resolved path is still inside
        // DocumentsRootPath as a second, independent check.
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return Task.FromResult<Stream?>(null);
        }

        var fullPath = Path.GetFullPath(Path.Combine(new[] { fullRootPath }.Concat(segments).ToArray()));

        if (!fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            // A tampered/out-of-root reference, or one whose file is
            // simply no longer there - both are "not available" (see
            // this method's doc comment), never an exception. A missing
            // government PDF is exactly Government Scenario F.
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
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

        // Every reference SaveImageAsync ever returns starts with this
        // fixed PublicBaseUrl prefix (see that method above) - anything
        // that doesn't isn't a reference this service issued, so there is
        // nothing safe to delete. Same "not available is a valid outcome,
        // never an exception" posture OpenDocumentAsync already takes.
        var prefix = _settings.PublicBaseUrl.TrimEnd('/') + "/";
        if (!imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relativePath = imageUrl[prefix.Length..];

        // Same defensive posture OpenDocumentAsync applies to a
        // caller-influenced path segment: reject ".."/"." before ever
        // touching the filesystem, then re-verify the fully-resolved path
        // is still inside RootPath as a second, independent check.
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.GetFullPath(Path.Combine(new[] { fullRootPath }.Concat(segments).ToArray()));

        if (!fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return Task.CompletedTask;
        }

        File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Shared by <see cref="SaveImageAsync"/>, <see cref="SaveDocumentAsync"/>
    /// and <see cref="SaveGovernmentDocumentAsync"/>:
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
