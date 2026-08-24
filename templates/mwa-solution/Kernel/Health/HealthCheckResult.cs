using System.Text.Json.Serialization;

namespace Kernel.Health;

/// <summary>
/// Represents the outcome of an individual health check execution.
/// </summary>
public record HealthCheckResult
{
    /// <summary>
    /// The health status returned by the check.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// An optional human-readable description of the health check outcome.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// An optional exception associated with a degraded or unhealthy state.
    /// </summary>
    [JsonIgnore]
    public Exception? Exception { get; init; }

    /// <summary>
    /// Additional diagnostic key-value data.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    public HealthCheckResult(
        HealthStatus status,
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        Status = status;
        Description = description;
        Exception = exception;
        Data = data;
    }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(string? description = null, IReadOnlyDictionary<string, object>? data = null)
        => new(HealthStatus.Healthy, description, null, data);

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    public static HealthCheckResult Degraded(string? description = null, Exception? exception = null, IReadOnlyDictionary<string, object>? data = null)
        => new(HealthStatus.Degraded, description, exception, data);

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(string? description = null, Exception? exception = null, IReadOnlyDictionary<string, object>? data = null)
        => new(HealthStatus.Unhealthy, description, exception, data);
}
