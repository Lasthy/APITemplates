namespace Kernel.Health;

/// <summary>
/// Aggregates health check results for a specific module.
/// </summary>
public record ModuleHealthReport
{
    /// <summary>
    /// The name of the module.
    /// </summary>
    public string ModuleName { get; init; }

    /// <summary>
    /// The aggregated health status across all components in the module.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// The total duration spent executing health checks for this module.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The individual health check components in this module.
    /// </summary>
    public IReadOnlyList<HealthReportComponent> Components { get; init; }

    public ModuleHealthReport(
        string moduleName,
        IReadOnlyList<HealthReportComponent> components,
        TimeSpan duration)
    {
        ModuleName = moduleName;
        Components = components;
        Duration = duration;

        // Calculate aggregated module status: Unhealthy > Degraded > Healthy
        var status = HealthStatus.Healthy;
        foreach (var component in components)
        {
            status = status.Combine(component.Status);
        }

        Status = status;
    }
}
