using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Health;

/// <summary>
/// Default implementation of <see cref="IHealthService"/> that executes registered module checks.
/// </summary>
public class HealthService : IHealthService
{
    private readonly IHealthCheckRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthService> _logger;

    public HealthService(
        IHealthCheckRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<HealthService> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var allRegistrations = _registry.GetRegistrations();
        var moduleReports = new Dictionary<string, ModuleHealthReport>(StringComparer.OrdinalIgnoreCase);

        foreach (var (moduleName, registrations) in allRegistrations)
        {
            var moduleReport = await ExecuteModuleChecksAsync(moduleName, registrations, cancellationToken);
            moduleReports[moduleName] = moduleReport;
        }

        totalStopwatch.Stop();
        return new HealthReport(moduleReports, totalStopwatch.Elapsed);
    }

    public async Task<ModuleHealthReport?> GetModuleHealthReportAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var registrations = _registry.GetRegistrations(moduleName);
        if (registrations.Count == 0)
        {
            return null;
        }

        return await ExecuteModuleChecksAsync(moduleName, registrations, cancellationToken);
    }

    public async Task<HealthStatus> GetOverallStatusAsync(CancellationToken cancellationToken = default)
    {
        var report = await GetHealthReportAsync(cancellationToken);
        return report.Status;
    }

    private async Task<ModuleHealthReport> ExecuteModuleChecksAsync(
        string moduleName,
        IReadOnlyList<HealthCheckRegistration> registrations,
        CancellationToken cancellationToken)
    {
        var moduleStopwatch = Stopwatch.StartNew();
        var components = new List<HealthReportComponent>();

        foreach (var registration in registrations)
        {
            var component = await ExecuteSingleCheckAsync(registration, cancellationToken);
            components.Add(component);
        }

        moduleStopwatch.Stop();
        return new ModuleHealthReport(moduleName, components, moduleStopwatch.Elapsed);
    }

    private async Task<HealthReportComponent> ExecuteSingleCheckAsync(
        HealthCheckRegistration registration,
        CancellationToken cancellationToken)
    {
        var checkStopwatch = Stopwatch.StartNew();
        string checkName = registration.Name ?? registration.ImplementationType?.Name ?? "HealthCheck";

        try
        {
            HealthCheckResult result;

            if (registration.Factory != null)
            {
                result = await registration.Factory(_serviceProvider, cancellationToken);
            }
            else if (registration.ImplementationType != null)
            {
                var instance = _serviceProvider.GetService(registration.ImplementationType) as IHealthCheck
                    ?? ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, registration.ImplementationType) as IHealthCheck;

                if (instance == null)
                {
                    checkStopwatch.Stop();
                    return new HealthReportComponent(
                        name: checkName,
                        status: HealthStatus.Unhealthy,
                        duration: checkStopwatch.Elapsed,
                        description: "Failed to instantiate health check implementation.",
                        error: $"Could not resolve type '{registration.ImplementationType.FullName}' as IHealthCheck.");
                }

                checkName = instance.Name;
                result = await instance.CheckHealthAsync(cancellationToken);
            }
            else
            {
                checkStopwatch.Stop();
                return new HealthReportComponent(
                    name: checkName,
                    status: HealthStatus.Unhealthy,
                    duration: checkStopwatch.Elapsed,
                    description: "Invalid health check registration.",
                    error: "Registration has neither ImplementationType nor Factory delegate.");
            }

            checkStopwatch.Stop();

            return new HealthReportComponent(
                name: checkName,
                status: result.Status,
                duration: checkStopwatch.Elapsed,
                description: result.Description,
                error: result.Exception?.Message,
                data: result.Data);
        }
        catch (Exception ex)
        {
            checkStopwatch.Stop();
            _logger.LogError(ex, "Health check '{CheckName}' for module '{ModuleName}' threw an unhandled exception.", checkName, registration.ModuleName);

            return new HealthReportComponent(
                name: checkName,
                status: HealthStatus.Unhealthy,
                duration: checkStopwatch.Elapsed,
                description: "An unhandled exception occurred while executing the health check.",
                error: ex.Message);
        }
    }
}
