namespace Kernel.Health;

/// <summary>
/// Service responsible for executing health checks and aggregating health reports across modules.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Executes all registered health checks across all modules and compiles a complete <see cref="HealthReport"/>.
    /// </summary>
    Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes health checks for a specific module and returns its <see cref="ModuleHealthReport"/>.
    /// </summary>
    Task<ModuleHealthReport?> GetModuleHealthReportAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the overall health status of the application without compiling a full report.
    /// </summary>
    Task<HealthStatus> GetOverallStatusAsync(CancellationToken cancellationToken = default);
}
