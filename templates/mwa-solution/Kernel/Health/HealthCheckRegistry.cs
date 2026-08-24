using System.Collections.Concurrent;
using System.Reflection;

namespace Kernel.Health;

/// <summary>
/// Thread-safe in-memory registry for health checks grouped by module.
/// </summary>
public class HealthCheckRegistry : IHealthCheckRegistry
{
    private readonly ConcurrentDictionary<string, List<HealthCheckRegistration>> _registrations = new(StringComparer.OrdinalIgnoreCase);

    public void Register<T>(string moduleName) where T : class, IHealthCheck
    {
        Register(moduleName, typeof(T));
    }

    public void Register(string moduleName, Type healthCheckType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(healthCheckType);

        if (!typeof(IHealthCheck).IsAssignableFrom(healthCheckType) || healthCheckType.IsAbstract || healthCheckType.IsInterface)
        {
            throw new ArgumentException($"Type '{healthCheckType.FullName}' must be a concrete class implementing IHealthCheck.", nameof(healthCheckType));
        }

        var list = _registrations.GetOrAdd(moduleName, _ => []);
        lock (list)
        {
            if (!list.Any(r => r.ImplementationType == healthCheckType))
            {
                list.Add(new HealthCheckRegistration(moduleName, healthCheckType));
            }
        }
    }

    public void Register(
        string moduleName,
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        var list = _registrations.GetOrAdd(moduleName, _ => []);
        lock (list)
        {
            list.Add(new HealthCheckRegistration(moduleName, name, factory));
        }
    }

    public void RegisterFromAssembly(string moduleName, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(assembly);

        var checkTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IHealthCheck).IsAssignableFrom(t))
            .ToList();

        foreach (var checkType in checkTypes)
        {
            Register(moduleName, checkType);
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<HealthCheckRegistration>> GetRegistrations()
    {
        return _registrations.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<HealthCheckRegistration>)kvp.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<HealthCheckRegistration> GetRegistrations(string moduleName)
    {
        if (_registrations.TryGetValue(moduleName, out var list))
        {
            lock (list)
            {
                return list.ToList();
            }
        }

        return [];
    }
}
