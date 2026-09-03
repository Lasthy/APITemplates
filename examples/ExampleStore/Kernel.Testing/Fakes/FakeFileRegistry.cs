using System.Collections.Concurrent;
using Kernel.Common;
using Kernel.Storage;

namespace Kernel.Testing.Fakes;

/// <summary>
/// In-memory test fake implementing <see cref="IFileRegistry"/>.
/// </summary>
public class FakeFileRegistry : IFileRegistry
{
    private readonly ConcurrentDictionary<DefaultId, FileRegistryEntry> _entries = new();

    /// <summary>
    /// When set to true, any register call will return a simulated failure.
    /// </summary>
    public bool SimulateFailureOnRegister { get; set; }

    /// <inheritdoc />
    public Task<Result<DefaultId>> RegisterAsync(
        string path,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        if (SimulateFailureOnRegister)
        {
            return Task.FromResult(Result.Failure<DefaultId>(new InvalidOperationException("Simulated register failure.")));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Failure<DefaultId>(new ArgumentException("Path cannot be empty.", nameof(path))));
        }

        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        var now = DateTime.UtcNow;

        if (!expiry.HasValue)
        {
            var existing = _entries.Values
                .Where(e => e.Path == normalizedPath && !e.IsExpired)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();

            if (existing != null)
            {
                return Task.FromResult(Result.Success(existing.Id));
            }
        }

        var entry = new FileRegistryEntry
        {
            Id = DefaultId.NewUlid(),
            Path = normalizedPath,
            CreatedAt = now,
            ExpiresAt = expiry.HasValue ? now.Add(expiry.Value) : null
        };

        _entries[entry.Id] = entry;
        return Task.FromResult(Result.Success(entry.Id));
    }

    /// <inheritdoc />
    public Task<Result<FileRegistryEntry>> GetAsync(
        DefaultId id,
        CancellationToken ct = default)
    {
        if (!_entries.TryGetValue(id, out var entry))
        {
            return Task.FromResult(Result.Failure<FileRegistryEntry>(new KeyNotFoundException($"File registry entry '{id}' not found.")));
        }

        if (entry.IsExpired)
        {
            return Task.FromResult(Result.Failure<FileRegistryEntry>(new InvalidOperationException($"File registry entry '{id}' has expired.")));
        }

        return Task.FromResult(Result.Success(entry));
    }

    /// <inheritdoc />
    public Task<Result> RevokeAsync(
        DefaultId id,
        CancellationToken ct = default)
    {
        _entries.TryRemove(id, out _);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> RevokeByPathAsync(
        string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result.Success());
        }

        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        var matchingKeys = _entries.Values
            .Where(e => e.Path == normalizedPath)
            .Select(e => e.Id)
            .ToList();

        foreach (var key in matchingKeys)
        {
            _entries.TryRemove(key, out _);
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Clears all registered entries.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }
}
