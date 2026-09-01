using Kernel.Caching;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="ICacheService"/> that stores cache entries in an in-memory dictionary.
/// Supports expiration simulation, prefix removal, and direct inspection of cached items in tests.
/// </summary>
public class FakeCacheService : ICacheService
{
    private record CacheEntry(object? Value, DateTimeOffset? Expiry);

    private readonly Dictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>
    /// Gets all unexpired cached keys and their values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> AllCachedItems
    {
        get
        {
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;
                return _store
                    .Where(kvp => kvp.Value.Expiry == null || kvp.Value.Expiry > now)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// Gets the number of unexpired entries currently in the cache.
    /// </summary>
    public int Count => AllCachedItems.Count;

    /// <summary>
    /// Checks whether the cache currently contains an unexpired key.
    /// </summary>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var entry))
            {
                if (entry.Expiry == null || entry.Expiry > DateTimeOffset.UtcNow)
                {
                    return true;
                }
                _store.Remove(key);
            }
            return false;
        }
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var entry))
            {
                if (entry.Expiry == null || entry.Expiry > DateTimeOffset.UtcNow)
                {
                    if (entry.Value is T typedValue)
                    {
                        return Task.FromResult<T?>(typedValue);
                    }
                    if (entry.Value is null)
                    {
                        return Task.FromResult<T?>(default);
                    }
                }
                else
                {
                    _store.Remove(key);
                }
            }
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        DateTimeOffset? expiryTime = expiry.HasValue ? DateTimeOffset.UtcNow.Add(expiry.Value) : null;

        lock (_lock)
        {
            _store[key] = new CacheEntry(value, expiryTime);
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var existing = await GetAsync<T>(key, ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = await factory();
        await SetAsync(key, created, expiry, ct);
        return created;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            _store.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        lock (_lock)
        {
            var keysToRemove = _store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _store.Remove(key);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
        }
    }
}
