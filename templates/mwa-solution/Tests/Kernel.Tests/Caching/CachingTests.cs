using Kernel.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kernel.Tests.Caching;

public class CachingTests
{
    private InMemoryCacheService CreateCacheService(bool enabled = true, string? prefix = null)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new CacheSettings(serializer) { AutoSave = false };
        settings.SetValue("Enabled", enabled);
        settings.SetValue("DefaultExpirationSeconds", 60);
        if (!string.IsNullOrEmpty(prefix))
        {
            settings.SetValue("KeyPrefix", prefix);
        }
        return new InMemoryCacheService(memoryCache, settings);
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_ShouldStoreAndRetrieveValue()
    {
        var cache = CreateCacheService();

        await cache.SetAsync("user:101", "Alice", TimeSpan.FromMinutes(5));
        var value = await cache.GetAsync<string>("user:101");

        Assert.Equal("Alice", value);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForCachedKey()
    {
        var cache = CreateCacheService();

        await cache.SetAsync("item:1", 42);

        Assert.NotNull(await cache.GetAsync<int?>("item:1"));
        Assert.Null(await cache.GetAsync<int?>("item:999"));
    }

    [Fact]
    public async Task RemoveAsync_ShouldEvictItem()
    {
        var cache = CreateStorage();

        await cache.SetAsync("token", "xyz");
        Assert.Equal("xyz", await cache.GetAsync<string>("token"));

        await cache.RemoveAsync("token");
        Assert.Null(await cache.GetAsync<string>("token"));
    }

    [Fact]
    public async Task RemoveByPrefixAsync_ShouldEvictMatchingKeys()
    {
        var cache = CreateStorage();

        await cache.SetAsync("catalog:category:1", "Electronics");
        await cache.SetAsync("catalog:category:2", "Books");
        await cache.SetAsync("users:1", "Bob");

        await cache.RemoveByPrefixAsync("catalog:category");

        Assert.Null(await cache.GetAsync<string>("catalog:category:1"));
        Assert.Null(await cache.GetAsync<string>("catalog:category:2"));
        Assert.Equal("Bob", await cache.GetAsync<string>("users:1"));
    }

    private InMemoryCacheService CreateStorage() => CreateCacheService();

    [Fact]
    public async Task GetOrCreateAsync_ShouldExecuteFactoryOnlyOnCacheMiss()
    {
        var cache = CreateCacheService();
        var factoryCallCount = 0;

        var val1 = await cache.GetOrCreateAsync("calc", () =>
        {
            factoryCallCount++;
            return Task.FromResult(100);
        });

        var val2 = await cache.GetOrCreateAsync("calc", () =>
        {
            factoryCallCount++;
            return Task.FromResult(200);
        });

        Assert.Equal(100, val1);
        Assert.Equal(100, val2);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public async Task WhenDisabled_GetAndSetShouldBypassCache()
    {
        var cache = CreateCacheService(enabled: false);

        await cache.SetAsync("key", "value");
        var result = await cache.GetAsync<string>("key");

        Assert.Null(result);
    }
}
