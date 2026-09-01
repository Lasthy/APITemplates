using Asp.Versioning;
using Kernel.Configuration;
using Kernel.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Versioning.Extensions;

/// <summary>
/// Extension methods for configuring API versioning in the Kernel and Host.
/// </summary>
public static class VersioningExtensions
{
    /// <summary>
    /// Registers API versioning services into the DI container using settings from <see cref="VersioningSettings"/>.
    /// </summary>
    public static IServiceCollection AddKernelVersioning(
        this IServiceCollection services,
        VersioningSettings settings,
        Action<ApiVersioningOptions>? configure = null)
    {
        var apiVersioningBuilder = services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = settings.GetDefaultApiVersion();
            options.AssumeDefaultVersionWhenUnspecified = settings.AssumeDefaultVersionWhenUnspecified;
            options.ReportApiVersions = settings.ReportApiVersions;
            options.ApiVersionReader = settings.CreateApiVersionReader();
            configure?.Invoke(options);
        });

        apiVersioningBuilder.AddMvc();
        apiVersioningBuilder.AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// Registers API versioning on the <see cref="WebApplicationBuilder"/>, resolving or registering <see cref="VersioningSettings"/>.
    /// </summary>
    public static void AddKernelVersioning(
        this WebApplicationBuilder builder,
        Action<ApiVersioningOptions>? configure = null)
    {
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        VersioningSettings? versioningSettings = null;
        try
        {
            versioningSettings = dynamicSettings?.GetSection<VersioningSettings>();
        }
        catch
        {
            // not registered yet
        }

        if (versioningSettings == null)
        {
            var jsonSerializer = builder.Services
                .Where(d => d.ServiceType == typeof(IJsonSerializer))
                .Select(d => d.ImplementationInstance as IJsonSerializer)
                .FirstOrDefault() ?? new JsonSerializer();
            var env = dynamicSettings?.Environment ?? builder.Environment.EnvironmentName;
            versioningSettings = new VersioningSettings(jsonSerializer, env);
            dynamicSettings?.AddSection(versioningSettings);
        }

        builder.Services.AddKernelVersioning(versioningSettings, configure);
    }
}
