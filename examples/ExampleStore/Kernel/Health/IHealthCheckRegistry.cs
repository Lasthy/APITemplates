using System.Reflection;

namespace Kernel.Health;

/// <summary>
/// Registry for managing module-scoped health check registrations.
/// </summary>
public interface IHealthCheckRegistry
{
    /// <summary>
    /// Registers a health check implementation type for a module.
    /// </summary>
    void Register<T>(string moduleName) where T : class, IHealthCheck;

    /// <summary>
    /// Registers a health check implementation type for a module.
    /// </summary>
    void Register(string moduleName, Type healthCheckType);

    /// <summary>
    /// Registers a delegate-based health check for a module.
    /// </summary>
    void Register(
        string moduleName,
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> factory);

    /// <summary>
    /// Scans an assembly and registers all non-abstract <see cref="IHealthCheck"/> implementations for a module.
    /// </summary>
    void RegisterFromAssembly(string moduleName, Assembly assembly);

    /// <summary>
    /// Gets all health check registrations grouped by module name.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<HealthCheckRegistration>> GetRegistrations();

    /// <summary>
    /// Gets all health check registrations for a specific module.
    /// </summary>
    IReadOnlyList<HealthCheckRegistration> GetRegistrations(string moduleName);
}
