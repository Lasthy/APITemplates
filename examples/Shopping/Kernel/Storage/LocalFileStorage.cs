using Kernel.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kernel.Storage;

/// <summary>
/// Default file storage provider that stores files on the local filesystem.
/// Features directory auto-creation, path traversal protection, and file size limits.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageSettings _settings;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(FileStorageSettings settings, ILogger<LocalFileStorage>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger<LocalFileStorage>.Instance;
    }

    /// <inheritdoc />
    public async Task<Result<string>> UploadAsync(
        string path,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Failure<string>(new ArgumentException("File path cannot be empty.", nameof(path)));
        }

        if (content == null)
        {
            return Result.Failure<string>(new ArgumentNullException(nameof(content)));
        }

        try
        {
            var normalizedPath = NormalizeRelativePath(path);
            var (isSafe, fullPath) = ResolveSafeFullPath(normalizedPath);
            if (!isSafe)
            {
                _logger.LogWarning("Potential path traversal attempt blocked for path: {Path}", path);
                return Result.Failure<string>(new ArgumentException("Invalid path: directory traversal attempt detected.", nameof(path)));
            }

            // Validate extension if restrictions configured
            var ext = Path.GetExtension(normalizedPath);
            if (_settings.AllowedExtensions.Count > 0 &&
                !_settings.AllowedExtensions.Any(e => string.Equals(e.TrimStart('.'), ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure<string>(new InvalidOperationException($"File extension '{ext}' is not allowed. Allowed extensions: {string.Join(", ", _settings.AllowedExtensions)}"));
            }

            // Check seekable stream length
            if (content.CanSeek && _settings.MaxFileSizeBytes > 0 && content.Length > _settings.MaxFileSizeBytes)
            {
                return Result.Failure<string>(new InvalidOperationException($"File size ({content.Length} bytes) exceeds maximum allowed size of {_settings.MaxFileSizeBytes} bytes."));
            }

            // Ensure destination directory exists
            if (_settings.AutoCreateDirectory)
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            // Stream to destination file with size tracking
            const int bufferSize = 81920;
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

            if (_settings.MaxFileSizeBytes > 0 && !content.CanSeek)
            {
                var buffer = new byte[bufferSize];
                long totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > _settings.MaxFileSizeBytes)
                    {
                        await fileStream.DisposeAsync();
                        try { File.Delete(fullPath); } catch { }
                        return Result.Failure<string>(new InvalidOperationException($"File exceeds maximum allowed size of {_settings.MaxFileSizeBytes} bytes."));
                    }
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                }
            }
            else
            {
                await content.CopyToAsync(fileStream, bufferSize, ct);
            }

            _logger.LogInformation("Successfully stored file at {StoragePath}", normalizedPath);
            return Result.Success<string>(normalizedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to {Path}", path);
            return Result.Failure<string>(ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<Stream>> DownloadAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Failure<Stream>(new ArgumentException("File path cannot be empty.", nameof(path))));
        }

        try
        {
            var normalizedPath = NormalizeRelativePath(path);
            var (isSafe, fullPath) = ResolveSafeFullPath(normalizedPath);
            if (!isSafe)
            {
                return Task.FromResult(Result.Failure<Stream>(new ArgumentException("Invalid path: directory traversal attempt detected.", nameof(path))));
            }

            if (!File.Exists(fullPath))
            {
                return Task.FromResult(Result.Failure<Stream>(new FileNotFoundException($"File '{normalizedPath}' not found.")));
            }

            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            return Task.FromResult(Result.Success<Stream>(fileStream));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from {Path}", path);
            return Task.FromResult(Result.Failure<Stream>(ex));
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Failure(new ArgumentException("File path cannot be empty.", nameof(path))));
        }

        try
        {
            var normalizedPath = NormalizeRelativePath(path);
            var (isSafe, fullPath) = ResolveSafeFullPath(normalizedPath);
            if (!isSafe)
            {
                return Task.FromResult(Result.Failure(new ArgumentException("Invalid path: directory traversal attempt detected.", nameof(path))));
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file at {StoragePath}", normalizedPath);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file at {Path}", path);
            return Task.FromResult(Result.Failure(ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<string>> GetUrlAsync(string path, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Failure<string>(new ArgumentException("File path cannot be empty.", nameof(path))));
        }

        var normalizedPath = NormalizeRelativePath(path);
        var baseUrl = _settings.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Task.FromResult(Result.Success<string>($"/{normalizedPath}"));
        }

        var url = $"{baseUrl.TrimEnd('/')}/{normalizedPath}";
        return Task.FromResult(Result.Success<string>(url));
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Success<bool>(false));
        }

        try
        {
            var normalizedPath = NormalizeRelativePath(path);
            var (isSafe, fullPath) = ResolveSafeFullPath(normalizedPath);
            if (!isSafe)
            {
                return Task.FromResult(Result.Success<bool>(false));
            }

            return Task.FromResult(Result.Success<bool>(File.Exists(fullPath)));
        }
        catch
        {
            return Task.FromResult(Result.Success<bool>(false));
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private (bool IsSafe, string FullPath) ResolveSafeFullPath(string relativePath)
    {
        var basePath = Path.GetFullPath(_settings.BasePath);
        var combined = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        // Ensure combined path starts with the configured base directory path
        var isSafe = combined.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        return (isSafe, combined);
    }
}
