using Kernel.Configuration;
using Kernel.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kernel.Caching.Extensions;

/// <summary>
/// Extension methods for configuring and registering caching infrastructure.
/// </summary>
public static class CacheExtensions
{
    /// <summary>
    /// Registers caching services, <see cref="CacheSettings"/>, and the default in-memory cache implementation
    /// on the <see cref="WebApplicationBuilder"/>.
    /// </summary>
    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        var jsonSerializer = builder.Services
            .Where(d => d.ServiceType == typeof(IJsonSerializer))
            .Select(d => d.ImplementationInstance as IJsonSerializer)
            .FirstOrDefault() ?? new JsonSerializer();

        var environment = dynamicSettings?.Environment ?? builder.Environment.EnvironmentName;
        var cacheSettings = new CacheSettings(jsonSerializer, environment);

        if (dynamicSettings != null)
        {
            dynamicSettings.AddSection(cacheSettings);
        }

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(cacheSettings);
        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
        builder.Services.AddTransient<CacheInterceptor>();

        return builder;
    }

    /// <summary>
    /// Registers caching services and in-memory cache implementation on <see cref="IServiceCollection"/>.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, CacheSettings? settings = null)
    {
        services.AddMemoryCache();

        if (settings != null)
        {
            services.AddSingleton(settings);
        }
        else
        {
            services.TryAddSingleton(sp =>
            {
                var dynamicSettings = sp.GetService<DynamicSettings>();
                var jsonSerializer = sp.GetService<IJsonSerializer>() ?? new JsonSerializer();
                return new CacheSettings(jsonSerializer, dynamicSettings?.Environment);
            });
        }

        services.TryAddSingleton<ICacheService, InMemoryCacheService>();
        services.TryAddTransient<CacheInterceptor>();

        return services;
    }
}
