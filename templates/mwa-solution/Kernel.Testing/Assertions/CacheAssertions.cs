using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeCacheService"/>.
/// </summary>
public static class CacheAssertions
{
    /// <summary>
    /// Asserts that the cache contains the specified key.
    /// </summary>
    public static void ShouldContainKey(
        this FakeCacheService cache,
        string key,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(key);

        Assert.True(
            cache.ContainsKey(key),
            userMessage ?? $"Expected cache to contain key '{key}', but it was not found.");
    }

    /// <summary>
    /// Asserts that the cache contains the specified key with the expected value.
    /// </summary>
    public static void ShouldContain<T>(
        this FakeCacheService cache,
        string key,
        T expectedValue,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(key);

        Assert.True(
            cache.ContainsKey(key),
            userMessage ?? $"Expected cache to contain key '{key}', but it was not found.");

        var actual = cache.GetAsync<T>(key).GetAwaiter().GetResult();
        Assert.Equal(expectedValue, actual);
    }

    /// <summary>
    /// Asserts that the cache does not contain the specified key.
    /// </summary>
    public static void ShouldNotContainKey(
        this FakeCacheService cache,
        string key,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(key);

        Assert.False(
            cache.ContainsKey(key),
            userMessage ?? $"Expected cache not to contain key '{key}', but it was present.");
    }

    /// <summary>
    /// Asserts that the cache is completely empty.
    /// </summary>
    public static void ShouldBeEmpty(this FakeCacheService cache, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        Assert.True(
            cache.Count == 0,
            userMessage ?? $"Expected cache to be empty, but it contained {cache.Count} entries.");
    }

    /// <summary>
    /// Asserts that the cache contains exactly <paramref name="expectedCount"/> items.
    /// </summary>
    public static void ShouldHaveCount(
        this FakeCacheService cache,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        Assert.True(
            cache.Count == expectedCount,
            userMessage ?? $"Expected cache to have {expectedCount} entries, but found {cache.Count}.");
    }
}
