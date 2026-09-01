using Kernel.Context;
using Kernel.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Caching;

/// <summary>
/// Applies automatic response caching to a request handler execution.
/// If a cached response exists, the handler execution is short-circuited and the cached
/// value is returned immediately as a successful result.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class CacheAttribute : RequestInterceptorAttribute
{
    /// <summary>
    /// Duration in seconds to keep the result in cache.
    /// If 0 or not specified, the value from <see cref="CacheSettings.DefaultExpirationSeconds"/> is used.
    /// </summary>
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// Key template with placeholders resolved from request properties (e.g. <c>"user:{request.Id}"</c>,
    /// <c>"product:{Id}:{CategoryId}"</c>, or <c>"{userId}"</c>).
    /// </summary>
    public string? KeyTemplate { get; set; }

    /// <summary>
    /// Optional prefix prepended to the generated cache key.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// When true, includes the current authenticated user's ID in the cache key, isolating cached values per user.
    /// </summary>
    public bool VaryByUser { get; set; } = false;

    public CacheAttribute()
    {
        Order = -25; // Runs after Authorization (-100) and Validation (-50), before Logging (-10) and Transaction (0)
    }

    public CacheAttribute(int durationSeconds) : this()
    {
        DurationSeconds = durationSeconds;
    }

    public CacheAttribute(string keyTemplate, int durationSeconds = 0) : this()
    {
        KeyTemplate = keyTemplate;
        DurationSeconds = durationSeconds;
    }

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var settings = serviceProvider.GetRequiredService<CacheSettings>();
        var scopeContext = serviceProvider.GetService<IScopeContext>();
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<CacheInterceptor>();

        return new CacheInterceptor(
            cacheService,
            settings,
            scopeContext,
            logger,
            DurationSeconds,
            KeyTemplate,
            KeyPrefix,
            VaryByUser);
    }
}
