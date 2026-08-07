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
