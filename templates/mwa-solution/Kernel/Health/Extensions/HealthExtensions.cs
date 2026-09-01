using Kernel.Health.Checks;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Health.Extensions;

/// <summary>
/// Extension methods for configuring and registering health check infrastructure.
/// </summary>
public static class HealthExtensions
{
    /// <summary>
    /// Registers the health check registry, health service, and kernel memory health check.
    /// </summary>
    public static IServiceCollection AddKernelHealthChecks(this IServiceCollection services)
    {
        var healthCheckRegistry = new HealthCheckRegistry();
        healthCheckRegistry.Register<MemoryHealthCheck>("Kernel");
        services.AddSingleton<IHealthCheckRegistry>(healthCheckRegistry);
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<MemoryHealthCheck>();
        return services;
    }
}
