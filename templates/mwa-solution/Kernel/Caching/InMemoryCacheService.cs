using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kernel.Caching;

/// <summary>
/// Default in-memory implementation of <see cref="ICacheService"/> backed by ASP.NET Core's <see cref="IMemoryCache"/>.
/// Supports active key tracking for prefix invalidation and key-level synchronization for stampede prevention.
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _settings;
    private readonly ILogger<InMemoryCacheService>? _logger;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryCacheService(
        IMemoryCache memoryCache,
        CacheSettings settings,
        ILogger<InMemoryCacheService>? logger = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_settings.Enabled)
        {
            return Task.FromResult<T?>(default);
        }

        var fullKey = GetFullKey(key);
        if (_memoryCache.TryGetValue(fullKey, out var raw) && raw is T value)
        {
            _logger?.LogDebug("Cache HIT for key: {Key}", fullKey);
            return Task.FromResult<T?>(value);
        }

        _logger?.LogDebug("Cache MISS for key: {Key}", fullKey);
        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_settings.Enabled || value == null)
        {
            return Task.CompletedTask;
        }

        var fullKey = GetFullKey(key);
        var duration = expiry ?? TimeSpan.FromSeconds(_settings.DefaultExpirationSeconds);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };

        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string k)
            {
                _trackedKeys.TryRemove(k, out _);
            }
        });

        _memoryCache.Set(fullKey, value, options);
        _trackedKeys.TryAdd(fullKey, 0);
        _logger?.LogDebug("Cached key: {Key} with duration: {Duration}", fullKey, duration);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_settings.Enabled)
        {
            return await factory();
        }

        var fullKey = GetFullKey(key);
        if (_memoryCache.TryGetValue(fullKey, out var raw) && raw is T existing)
        {
            _logger?.LogDebug("Cache HIT for key: {Key}", fullKey);
            return existing;
        }

        var semaphore = _locks.GetOrAdd(fullKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            if (_memoryCache.TryGetValue(fullKey, out raw) && raw is T recheck)
            {
                _logger?.LogDebug("Cache HIT (after lock) for key: {Key}", fullKey);
                return recheck;
            }

            _logger?.LogDebug("Cache MISS for key: {Key}, invoking factory", fullKey);
            var value = await factory();

            if (value != null)
            {
                var duration = expiry ?? TimeSpan.FromSeconds(_settings.DefaultExpirationSeconds);
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = duration
                };

                options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
                {
                    if (evictedKey is string k)
                    {
                        _trackedKeys.TryRemove(k, out _);
                    }
                });

                _memoryCache.Set(fullKey, value, options);
                _trackedKeys.TryAdd(fullKey, 0);
            }

            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        _memoryCache.Remove(fullKey);
        _trackedKeys.TryRemove(fullKey, out _);
        _logger?.LogDebug("Removed cached key: {Key}", fullKey);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var fullPrefix = GetFullKey(prefix);
        var matchingKeys = _trackedKeys.Keys
            .Where(k => k.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var k in matchingKeys)
        {
            _memoryCache.Remove(k);
            _trackedKeys.TryRemove(k, out _);
        }

        _logger?.LogDebug("Removed {Count} cached keys matching prefix: {Prefix}", matchingKeys.Count, fullPrefix);
        return Task.CompletedTask;
    }

    private string GetFullKey(string key)
    {
        return string.IsNullOrEmpty(_settings.KeyPrefix)
            ? key
            : $"{_settings.KeyPrefix}:{key}";
    }
}
