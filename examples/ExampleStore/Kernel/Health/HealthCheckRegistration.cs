using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Health;

/// <summary>
/// Represents a health check registration associated with a specific module.
/// </summary>
public record HealthCheckRegistration
{
    /// <summary>
    /// The name of the module that owns this health check.
    /// </summary>
    public string ModuleName { get; init; }

    /// <summary>
    /// The concrete implementation type of the <see cref="IHealthCheck"/>, if registered by type.
    /// </summary>
    public Type? ImplementationType { get; init; }

    /// <summary>
    /// An optional delegate factory for executing the health check directly.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>>? Factory { get; init; }

    /// <summary>
    /// Explicit name for delegate-based registrations.
    /// </summary>
    public string? Name { get; init; }

    public HealthCheckRegistration(string moduleName, Type implementationType)
    {
        ModuleName = moduleName;
        ImplementationType = implementationType;
    }

    public HealthCheckRegistration(
        string moduleName,
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> factory)
    {
        ModuleName = moduleName;
        Name = name;
        Factory = factory;
    }
}
