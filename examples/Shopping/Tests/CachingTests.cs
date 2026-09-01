using System.Security.Claims;
using Kernel.Authorization;
using Kernel.Authorization.Extensions;
using Kernel.Caching;
using Kernel.Caching.Extensions;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Context;
using Kernel.Json;
using Kernel.Requests;
using Kernel.Validation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Shopping.Tests;

public class CachingTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public CachingTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    #region Test Models, Handlers, and Helpers

    public record TestProductDto(int Id, string Name, decimal Price);
    public record FilterCriteria(string Status, string Category);

    public record GetProductRequest(int Id) : IRequest<TestProductDto>;
    public record GetProductNestedRequest(FilterCriteria Filter, int Page) : IRequest<string>;
    public record GetUserProfileRequest(string Section) : IRequest<string>;
    public record GetWithRequestPrefixRequest(string Code) : IRequest<string>;
    public record FailOnConditionRequest(bool ShouldFail, string Data) : IRequest<string>;
    public record SecuredCachedRequest(int Id, string Value) : IRequest<string>;

    [Cache(DurationSeconds = 60, KeyTemplate = "product:{Id}")]
    public class GetProductHandler : IRequestHandler<GetProductRequest, TestProductDto>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<TestProductDto>> HandleAsync(GetProductRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(Result.Success<TestProductDto>(new TestProductDto(request.Id, $"Product {request.Id}", 19.99m * request.Id)));
        }
    }

    [Cache(KeyTemplate = "catalog:{Filter.Category}:{Filter.Status}:p{Page}")]
    public class GetProductNestedHandler : IRequestHandler<GetProductNestedRequest, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<string>> HandleAsync(GetProductNestedRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(Result.Success<string>($"Category={request.Filter.Category}, Status={request.Filter.Status}, Page={request.Page}"));
        }
    }

    [Cache(KeyTemplate = "profile:{userId}:{Section}", VaryByUser = true)]
    public class GetUserProfileHandler : IRequestHandler<GetUserProfileRequest, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<string>> HandleAsync(GetUserProfileRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(Result.Success<string>($"ProfileSection={request.Section}"));
        }
    }

    [Cache(KeyTemplate = "prefixed:{request.Code}", KeyPrefix = "custom")]
    public class GetWithRequestPrefixHandler : IRequestHandler<GetWithRequestPrefixRequest, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<string>> HandleAsync(GetWithRequestPrefixRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(Result.Success<string>($"Result for code {request.Code}"));
        }
    }

    [Cache(KeyTemplate = "conditional:{Data}")]
    public class FailOnConditionHandler : IRequestHandler<FailOnConditionRequest, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<string>> HandleAsync(FailOnConditionRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            if (request.ShouldFail)
            {
                return Task.FromResult(Result.Failure<string>(new InvalidOperationException("Failed intentionally.")));
            }

            return Task.FromResult(Result.Success<string>($"Success: {request.Data}"));
        }
    }

    public class SecuredCachedRequestValidator : IValidator<SecuredCachedRequest>
    {
        public ValidationResult Validate(SecuredCachedRequest request)
        {
            if (request.Id <= 0)
                return ValidationResult.Failure("Id", "Id must be positive.");
            return ValidationResult.Success();
        }
    }

    [Authorize("Authenticated")]
    [Validate]
    [Cache(KeyTemplate = "secured:{Id}")]
    public class SecuredCachedHandler : IRequestHandler<SecuredCachedRequest, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<Result<string>> HandleAsync(SecuredCachedRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(Result.Success<string>($"Secured data for {request.Id}: {request.Value}"));
        }
    }

    public class TestScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public bool IsSystem => IsInRole("System");
        public string? CorrelationId { get; set; } = "test-corr-id";

        public string? GetClaim(string claimType) => Claims.TryGetValue(claimType, out var v) ? v : null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => Claims.TryGetValue(claimType, out var v) ? [v] : [];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static IServiceProvider CreateTestServiceProvider(Action<ServiceCollection>? configure = null, CacheSettings? cacheSettings = null)
    {
        var services = new ServiceCollection();
        var jsonSerializer = new JsonSerializer();

        services.AddSingleton<IJsonSerializer>(jsonSerializer);
        services.AddMemoryCache();

        var settings = cacheSettings ?? new CacheSettings(jsonSerializer);
        services.AddSingleton(settings);
        services.AddSingleton<ICacheService, InMemoryCacheService>();

        services.AddScoped<IScopeContext, TestScopeContext>();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<CacheInterceptor>();
        services.AddTransient<ValidationInterceptor>();
        services.AddAuthorizationPolicies(typeof(CachingTests).Assembly);

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    #endregion

    #region Direct ICacheService Tests

    [Fact]
    public async Task SetAsync_And_GetAsync_StoresAndRetrievesValues()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        // Primitive string
        await cache.SetAsync("key:string", "hello world");
        var strVal = await cache.GetAsync<string>("key:string");
        Assert.Equal("hello world", strVal);

        // Complex object
        var product = new TestProductDto(1, "Smartphone", 499.99m);
        await cache.SetAsync("key:product:1", product);
        var prodVal = await cache.GetAsync<TestProductDto>("key:product:1");
        Assert.NotNull(prodVal);
        Assert.Equal(1, prodVal.Id);
        Assert.Equal("Smartphone", prodVal.Name);
        Assert.Equal(499.99m, prodVal.Price);
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        var result = await cache.GetAsync<string>("non:existent:key");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenNotCached_CallsFactoryAndCaches()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        var factoryCount = 0;
        Task<string> Factory()
        {
            factoryCount++;
            return Task.FromResult("generated value");
        }

        var first = await cache.GetOrCreateAsync("test:factory", Factory);
        Assert.Equal("generated value", first);
        Assert.Equal(1, factoryCount);

        // Second call should return cached value without executing factory
        var second = await cache.GetOrCreateAsync("test:factory", Factory);
        Assert.Equal("generated value", second);
        Assert.Equal(1, factoryCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_StampedeProtection_InvokesFactoryOnceUnderConcurrency()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        var factoryCount = 0;
        async Task<int> SlowFactory()
        {
            Interlocked.Increment(ref factoryCount);
            await Task.Delay(50);
            return 42;
        }

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.GetOrCreateAsync("test:stampede", SlowFactory))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, val => Assert.Equal(42, val));
        Assert.Equal(1, factoryCount);
    }

    [Fact]
    public async Task RemoveAsync_RemovesKeyFromCache()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        await cache.SetAsync("item:to:remove", "temporary");
        Assert.Equal("temporary", await cache.GetAsync<string>("item:to:remove"));

        await cache.RemoveAsync("item:to:remove");
        Assert.Null(await cache.GetAsync<string>("item:to:remove"));
    }

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesMatchingPrefixOnly()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        await cache.SetAsync("catalog:products:1", "p1");
        await cache.SetAsync("catalog:products:2", "p2");
        await cache.SetAsync("catalog:categories:1", "c1");
        await cache.SetAsync("user:1", "u1");

        // Remove only catalog:products
        await cache.RemoveByPrefixAsync("catalog:products");

        Assert.Null(await cache.GetAsync<string>("catalog:products:1"));
        Assert.Null(await cache.GetAsync<string>("catalog:products:2"));
        Assert.Equal("c1", await cache.GetAsync<string>("catalog:categories:1"));
        Assert.Equal("u1", await cache.GetAsync<string>("user:1"));
    }

    [Fact]
    public async Task SetAsync_WithExpiration_ExpiresAfterTtl()
    {
        var sp = CreateTestServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        await cache.SetAsync("short:lived", "expires soon", TimeSpan.FromMilliseconds(50));
        Assert.Equal("expires soon", await cache.GetAsync<string>("short:lived"));

        await Task.Delay(100);

        Assert.Null(await cache.GetAsync<string>("short:lived"));
    }

    [Fact]
    public async Task CacheService_WhenDisabled_BypassesAllOperations()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"appsettings.Cache.{Guid.NewGuid():N}.json");
        File.WriteAllText(tempFile, "{\"Enabled\": false}");

        try
        {
            var jsonSerializer = new JsonSerializer();
            var settings = new CacheSettings(tempFile, jsonSerializer);

            var sp = CreateTestServiceProvider(cacheSettings: settings);
            var cache = sp.GetRequiredService<ICacheService>();

            await cache.SetAsync("disabled:key", "value");
            Assert.Null(await cache.GetAsync<string>("disabled:key"));

            var factoryCount = 0;
            var val1 = await cache.GetOrCreateAsync("disabled:factory", () =>
            {
                factoryCount++;
                return Task.FromResult("val");
            });
            var val2 = await cache.GetOrCreateAsync("disabled:factory", () =>
            {
                factoryCount++;
                return Task.FromResult("val");
            });

            Assert.Equal("val", val1);
            Assert.Equal("val", val2);
            Assert.Equal(2, factoryCount);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Dispatcher & [Cache] Interceptor Tests

    [Fact]
    public async Task Dispatcher_WithCacheAttribute_CachesResponseOnFirstCallAndReusesOnSecond()
    {
        GetProductHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<GetProductRequest, TestProductDto>, GetProductHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // First call
        var result1 = await dispatcher.DispatchAsync(new GetProductRequest(10));
        Assert.True(result1.IsSuccess);
        Assert.NotNull(result1.Value);
        Assert.Equal(10, result1.Value.Id);
        Assert.Equal("Product 10", result1.Value.Name);
        Assert.Equal(1, GetProductHandler.ExecutionCount);

        // Second call with same request should be served from cache
        var result2 = await dispatcher.DispatchAsync(new GetProductRequest(10));
        Assert.True(result2.IsSuccess);
        Assert.NotNull(result2.Value);
        Assert.Equal(10, result2.Value.Id);
        Assert.Equal("Product 10", result2.Value.Name);
        Assert.Equal(1, GetProductHandler.ExecutionCount);

        // Call with different ID executes handler again
        var result3 = await dispatcher.DispatchAsync(new GetProductRequest(20));
        Assert.True(result3.IsSuccess);
        Assert.NotNull(result3.Value);
        Assert.Equal(20, result3.Value.Id);
        Assert.Equal(2, GetProductHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_WithCacheAttribute_ResolvesNestedPropertiesInKeyTemplate()
    {
        GetProductNestedHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<GetProductNestedRequest, string>, GetProductNestedHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var req1 = new GetProductNestedRequest(new FilterCriteria("Active", "Electronics"), 1);
        var res1 = await dispatcher.DispatchAsync(req1);
        Assert.True(res1.IsSuccess);
        Assert.Equal("Category=Electronics, Status=Active, Page=1", res1.Value);
        Assert.Equal(1, GetProductNestedHandler.ExecutionCount);

        // Repeat call matches key
        var res2 = await dispatcher.DispatchAsync(req1);
        Assert.True(res2.IsSuccess);
        Assert.Equal(1, GetProductNestedHandler.ExecutionCount);

        // Changing nested property triggers new execution
        var req2 = new GetProductNestedRequest(new FilterCriteria("Inactive", "Electronics"), 1);
        var res3 = await dispatcher.DispatchAsync(req2);
        Assert.True(res3.IsSuccess);
        Assert.Equal(2, GetProductNestedHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_WithCacheAttribute_ResolvesUserIdAndVaryByUser()
    {
        GetUserProfileHandler.ExecutionCount = 0;
        var user1Id = DefaultId.NewUlid();
        var user2Id = DefaultId.NewUlid();

        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<GetUserProfileRequest, string>, GetUserProfileHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var scopeContext = (TestScopeContext)sp.GetRequiredService<IScopeContext>();

        // Set User 1
        scopeContext.UserId = user1Id;
        var res1 = await dispatcher.DispatchAsync(new GetUserProfileRequest("settings"));
        Assert.True(res1.IsSuccess);
        Assert.Equal(1, GetUserProfileHandler.ExecutionCount);

        // Same User 1 -> cache hit
        var res2 = await dispatcher.DispatchAsync(new GetUserProfileRequest("settings"));
        Assert.True(res2.IsSuccess);
        Assert.Equal(1, GetUserProfileHandler.ExecutionCount);

        // Switch to User 2 -> cache miss, executes handler
        scopeContext.UserId = user2Id;
        var res3 = await dispatcher.DispatchAsync(new GetUserProfileRequest("settings"));
        Assert.True(res3.IsSuccess);
        Assert.Equal(2, GetUserProfileHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_WithCacheAttribute_SupportsRequestPrefixAndCustomKeyPrefix()
    {
        GetWithRequestPrefixHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<GetWithRequestPrefixRequest, string>, GetWithRequestPrefixHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var cache = sp.GetRequiredService<ICacheService>();

        var res1 = await dispatcher.DispatchAsync(new GetWithRequestPrefixRequest("ABC123"));
        Assert.True(res1.IsSuccess);
        Assert.Equal(1, GetWithRequestPrefixHandler.ExecutionCount);

        // Verify direct cache lookup with formatted key "custom:prefixed:ABC123"
        var cachedVal = await cache.GetAsync<string>("custom:prefixed:ABC123");
        Assert.Equal("Result for code ABC123", cachedVal);

        // Subsequent call is cached
        var res2 = await dispatcher.DispatchAsync(new GetWithRequestPrefixRequest("ABC123"));
        Assert.True(res2.IsSuccess);
        Assert.Equal(1, GetWithRequestPrefixHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_WithCacheAttribute_DoesNotCacheFailures()
    {
        FailOnConditionHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<FailOnConditionRequest, string>, FailOnConditionHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Call with failure
        var failResult1 = await dispatcher.DispatchAsync(new FailOnConditionRequest(true, "test-data"));
        Assert.True(failResult1.IsFailure);
        Assert.Equal(1, FailOnConditionHandler.ExecutionCount);

        // Second call with same failure still runs handler
        var failResult2 = await dispatcher.DispatchAsync(new FailOnConditionRequest(true, "test-data"));
        Assert.True(failResult2.IsFailure);
        Assert.Equal(2, FailOnConditionHandler.ExecutionCount);

        // Now call with success
        var successResult1 = await dispatcher.DispatchAsync(new FailOnConditionRequest(false, "test-data"));
        Assert.True(successResult1.IsSuccess);
        Assert.Equal(3, FailOnConditionHandler.ExecutionCount);

        // Next call should be served from cache
        var successResult2 = await dispatcher.DispatchAsync(new FailOnConditionRequest(false, "test-data"));
        Assert.True(successResult2.IsSuccess);
        Assert.Equal(3, FailOnConditionHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_CacheInvalidation_AllowsReExecutionAfterKeyRemoval()
    {
        GetProductHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IRequestHandler<GetProductRequest, TestProductDto>, GetProductHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var cache = sp.GetRequiredService<ICacheService>();

        await dispatcher.DispatchAsync(new GetProductRequest(99));
        Assert.Equal(1, GetProductHandler.ExecutionCount);

        // Manually invalidate cache key
        await cache.RemoveAsync("product:99");

        // Next dispatch should execute handler again
        await dispatcher.DispatchAsync(new GetProductRequest(99));
        Assert.Equal(2, GetProductHandler.ExecutionCount);
    }

    [Fact]
    public async Task Dispatcher_PipelineOrder_RunsAfterAuthorizationAndValidation()
    {
        SecuredCachedHandler.ExecutionCount = 0;
        var sp = CreateTestServiceProvider(services =>
        {
            services.AddScoped<IValidator<SecuredCachedRequest>, SecuredCachedRequestValidator>();
            services.AddScoped<IRequestHandler<SecuredCachedRequest, string>, SecuredCachedHandler>();
        });

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var scopeContext = (TestScopeContext)sp.GetRequiredService<IScopeContext>();

        // 1. Unauthenticated -> Fails at Authorize interceptor
        scopeContext.UserId = null;
        var authFail = await dispatcher.DispatchAsync(new SecuredCachedRequest(1, "secret"));
        Assert.True(authFail.IsFailure);
        Assert.Equal(0, SecuredCachedHandler.ExecutionCount);

        // 2. Authenticated but Invalid -> Fails at Validate interceptor
        scopeContext.UserId = DefaultId.NewUlid();
        var valFail = await dispatcher.DispatchAsync(new SecuredCachedRequest(0, "invalid-id"));
        Assert.True(valFail.IsFailure);
        Assert.Equal(0, SecuredCachedHandler.ExecutionCount);

        // 3. Authenticated & Valid -> Passes to Handler and gets cached
        var success1 = await dispatcher.DispatchAsync(new SecuredCachedRequest(1, "valid-data"));
        Assert.True(success1.IsSuccess);
        Assert.Equal(1, SecuredCachedHandler.ExecutionCount);

        // 4. Subsequent valid call hits cache
        var success2 = await dispatcher.DispatchAsync(new SecuredCachedRequest(1, "valid-data"));
        Assert.True(success2.IsSuccess);
        Assert.Equal(1, SecuredCachedHandler.ExecutionCount);
    }

    #endregion

    #region CacheSettings Dynamic Configuration Tests

    [Fact]
    public void CacheSettings_DefaultValues_AreCorrect()
    {
        var jsonSerializer = new JsonSerializer();
        var settings = new CacheSettings(jsonSerializer);

        Assert.Equal("Cache", settings.SectionName);
        Assert.True(settings.Enabled);
        Assert.Equal(300, settings.DefaultExpirationSeconds);
        Assert.Equal(string.Empty, settings.KeyPrefix);
    }

    [Fact]
    public void CacheSettings_DynamicMutation_UpdatesProperties()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"appsettings.Cache.{Guid.NewGuid():N}.json");
        File.WriteAllText(tempFile, "{}");

        try
        {
            var jsonSerializer = new JsonSerializer();
            var settings = new CacheSettings(tempFile, jsonSerializer);

            settings.SetValue(nameof(CacheSettings.DefaultExpirationSeconds), 600);
            settings.SetValue(nameof(CacheSettings.KeyPrefix), "mwa");

            Assert.Equal(600, settings.DefaultExpirationSeconds);
            Assert.Equal("mwa", settings.KeyPrefix);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Web Host DI Integration Tests

    [Fact]
    public void Host_Registers_ICacheService_And_CacheSettings()
    {
        var cacheService = _factory.Services.GetService<ICacheService>();
        var cacheSettings = _factory.Services.GetService<CacheSettings>();
        var memoryCache = _factory.Services.GetService<IMemoryCache>();

        Assert.NotNull(cacheService);
        Assert.IsType<InMemoryCacheService>(cacheService);
        Assert.NotNull(cacheSettings);
        Assert.NotNull(memoryCache);
    }

    #endregion
}
