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

        var propertyFolder = Path.Combine(rootPath, propertyId.ToString());
        Directory.CreateDirectory(propertyFolder);

        // A generated name, not the caller-supplied one - never trust a
        // client-provided filename for a path segment. The original
        // extension is kept only for a friendlier file listing.
        var extension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(propertyFolder, storedFileName);

        // The upload is written to disk and SHA-256 hashed in the same
        // pass via CryptoStream, so fraud rule 2's fingerprint (see
        // ImageHash's doc comment on PropertyImage) never requires a
        // second read of the file.
        using var sha256 = SHA256.Create();

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        await using (var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write, leaveOpen: true))
        {
            await content.CopyToAsync(hashingStream, cancellationToken);
        }

        var hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        var url = $"{_settings.PublicBaseUrl.TrimEnd('/')}/{propertyId}/{storedFileName}";

        return new StoredImageFile(url, hash);
    }
}
