namespace Kernel.Caching;

/// <summary>
/// Abstraction for caching data with support for TTL expiration, key removal, prefix-based invalidation,
/// and atomic get-or-create operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached item by key. Returns <c>default</c> (or <c>null</c>) if not found or expired.
    /// </summary>
    /// <typeparam name="T">The type of the cached item.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached item or default if not present.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores an item in the cache with an optional expiration time.
    /// If <paramref name="expiry"/> is not specified, the configured default TTL is used.
    /// </summary>
    /// <typeparam name="T">The type of the item to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiry">Optional expiration duration.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves an item from cache if present; otherwise, executes the factory delegate,
    /// stores the result in cache, and returns it.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Async factory function to produce the value if not in cache.</param>
    /// <param name="expiry">Optional expiration duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The existing or newly generated value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>
    /// Removes an item from the cache by key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all cached items whose keys start with the given prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to match.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
