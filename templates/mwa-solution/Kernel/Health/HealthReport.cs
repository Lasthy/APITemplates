namespace Kernel.Health;

/// <summary>
/// Aggregates health reports across all modules in the application.
/// </summary>
public record HealthReport
{
    /// <summary>
    /// The overall health status of the entire application.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// The total duration spent executing all health checks across all modules.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// The UTC timestamp when the health report was generated.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Per-module health reports, keyed by module name.
    /// </summary>
    public IReadOnlyDictionary<string, ModuleHealthReport> Modules { get; init; }

    public HealthReport(
        IReadOnlyDictionary<string, ModuleHealthReport> modules,
        TimeSpan totalDuration,
        DateTime? timestamp = null)
    {
        Modules = modules;
        TotalDuration = totalDuration;
        Timestamp = timestamp ?? DateTime.UtcNow;

        // Calculate overall system status: Unhealthy > Degraded > Healthy
        var status = HealthStatus.Healthy;
        foreach (var (_, moduleReport) in modules)
        {
            status = status.Combine(moduleReport.Status);
        }

        Status = status;
    }
}
