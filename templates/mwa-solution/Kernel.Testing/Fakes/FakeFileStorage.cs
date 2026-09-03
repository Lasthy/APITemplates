using System.Text;
using Kernel.Common;
using Kernel.Storage;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="IFileStorage"/> that stores file blobs in an in-memory dictionary.
/// Provides inspection of stored and deleted files, call counts, and failure simulation for testing.
/// </summary>
public class FakeFileStorage : IFileStorage
{
    public record StoredFile(byte[] Content, string? ContentType, DateTimeOffset UploadedAt);

    private readonly Dictionary<string, StoredFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _deletedPaths = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets a snapshot of all currently stored files and their raw byte content.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Files
    {
        get
        {
            lock (_lock)
            {
                return _files.ToDictionary(k => k.Key, v => v.Value.Content, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Gets a list of all paths that have been deleted during the lifetime of this fake.
    /// </summary>
    public IReadOnlyList<string> DeletedPaths
    {
        get
        {
            lock (_lock)
            {
                return _deletedPaths.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of files currently stored.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _files.Count;
            }
        }
    }

    /// <summary>
    /// Total number of upload operations invoked.
    /// </summary>
    public int UploadCount { get; private set; }

    /// <summary>
    /// Total number of download operations invoked.
    /// </summary>
    public int DownloadCount { get; private set; }

    /// <summary>
    /// Total number of delete operations invoked.
    /// </summary>
    public int DeleteCount { get; private set; }

    /// <summary>
    /// When set to true, any upload call will return a failure result.
    /// </summary>
    public bool SimulateFailureOnUpload { get; set; }

    /// <summary>
    /// When set to true, any download call will return a failure result.
    /// </summary>
    public bool SimulateFailureOnDownload { get; set; }

    /// <summary>
    /// When set to true, any delete call will return a failure result.
    /// </summary>
    public bool SimulateFailureOnDelete { get; set; }

    /// <summary>
    /// The associated file registry. Defaults to an in-memory <see cref="FakeFileRegistry"/>.
    /// </summary>
    public IFileRegistry Registry { get; set; } = new FakeFileRegistry();

    /// <summary>
    /// Whether public file retrieval via generated file URLs is enabled. Defaults to true.
    /// </summary>
    public bool EnablePublicAccess { get; set; } = true;

    /// <inheritdoc />
    public async Task<Result<string>> UploadAsync(
        string path,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        lock (_lock)
        {
            UploadCount++;
        }

        if (SimulateFailureOnUpload)
        {
            return Result.Failure<string>(new InvalidOperationException("Simulated upload failure."));
        }

        var normalizedPath = NormalizePath(path);
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();

        lock (_lock)
        {
            _files[normalizedPath] = new StoredFile(bytes, contentType, DateTimeOffset.UtcNow);
        }

        return Result.Success<string>(normalizedPath);
    }

    /// <inheritdoc />
    public Task<Result<Stream>> DownloadAsync(string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        lock (_lock)
        {
            DownloadCount++;
        }

        if (SimulateFailureOnDownload)
        {
            return Task.FromResult(Result.Failure<Stream>(new InvalidOperationException("Simulated download failure.")));
        }

        var normalizedPath = NormalizePath(path);

        lock (_lock)
        {
            if (!_files.TryGetValue(normalizedPath, out var storedFile))
            {
                return Task.FromResult(Result.Failure<Stream>(new FileNotFoundException($"File '{normalizedPath}' not found.")));
            }

            var stream = new MemoryStream(storedFile.Content, writable: false);
            return Task.FromResult(Result.Success<Stream>(stream));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        lock (_lock)
        {
            DeleteCount++;
        }

        if (SimulateFailureOnDelete)
        {
            return Result.Failure(new InvalidOperationException("Simulated delete failure."));
        }

        var normalizedPath = NormalizePath(path);

        lock (_lock)
        {
            _files.Remove(normalizedPath);
            _deletedPaths.Add(normalizedPath);
        }

        await Registry.RevokeByPathAsync(normalizedPath, ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetUrlAsync(string path, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!EnablePublicAccess)
        {
            return Result.Failure<string>(new InvalidOperationException("Public file access is disabled in storage settings."));
        }

        var normalizedPath = NormalizePath(path);
        lock (_lock)
        {
            if (!_files.ContainsKey(normalizedPath))
            {
                return Result.Failure<string>(new FileNotFoundException($"File not found at '{normalizedPath}'.", normalizedPath));
            }
        }

        var regResult = await Registry.RegisterAsync(normalizedPath, expiry, ct);
        if (regResult.IsFailure)
        {
            return Result.Failure<string>(regResult.Error!);
        }

        return Result.Success<string>($"/files/{regResult.Value}");
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Success<bool>(false));
        }

        var normalizedPath = NormalizePath(path);

        lock (_lock)
        {
            return Task.FromResult(Result.Success<bool>(_files.ContainsKey(normalizedPath)));
        }
    }

    /// <summary>
    /// Checks whether the fake storage contains a file at the specified path.
    /// </summary>
    public bool ContainsFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        lock (_lock)
        {
            return _files.ContainsKey(normalizedPath);
        }
    }

    /// <summary>
    /// Retrieves the raw byte content of a stored file, or null if not found.
    /// </summary>
    public byte[]? GetFileBytes(string path)
    {
        var normalizedPath = NormalizePath(path);
        lock (_lock)
        {
            return _files.TryGetValue(normalizedPath, out var file) ? file.Content : null;
        }
    }

    /// <summary>
    /// Retrieves the text content of a stored file, or null if not found.
    /// </summary>
    public string? GetFileText(string path, Encoding? encoding = null)
    {
        var bytes = GetFileBytes(path);
        return bytes == null ? null : (encoding ?? Encoding.UTF8).GetString(bytes);
    }

    /// <summary>
    /// Clears all files and tracking history.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _files.Clear();
            _deletedPaths.Clear();
            UploadCount = 0;
            DownloadCount = 0;
            DeleteCount = 0;
            SimulateFailureOnUpload = false;
            SimulateFailureOnDownload = false;
            SimulateFailureOnDelete = false;
        }

        (Registry as FakeFileRegistry)?.Clear();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
