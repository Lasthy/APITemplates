namespace Kernel.Health;

/// <summary>
/// Represents the execution result and metadata of a single health check component within a module.
/// </summary>
public record HealthReportComponent
{
    /// <summary>
    /// The unique name of the health check component.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The health status of the component.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// An optional description of the component status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The execution duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Additional diagnostic data returned by the health check.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    /// <summary>
    /// Error message or exception details if the component is degraded or unhealthy.
    /// </summary>
    public string? Error { get; init; }

    public HealthReportComponent(
        string name,
        HealthStatus status,
        TimeSpan duration,
        string? description = null,
        string? error = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        Name = name;
        Status = status;
        Duration = duration;
        Description = description;
        Error = error;
        Data = data;
    }
}
