using System.Text.Json.Serialization;

namespace Kernel.Health;

/// <summary>
/// Represents the health status of a component, module, or the entire application.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HealthStatus
{
    /// <summary>
    /// The component or system is operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The component or system is functioning in a degraded state.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The component or system is in an unhealthy state and cannot fulfill requests properly.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Helper extensions for <see cref="HealthStatus"/>.
/// </summary>
public static class HealthStatusExtensions
{
    /// <summary>
    /// Combines two health statuses, returning the worse of the two (Unhealthy > Degraded > Healthy).
    /// </summary>
    public static HealthStatus Combine(this HealthStatus current, HealthStatus other)
    {
        return (HealthStatus)Math.Max((int)current, (int)other);
    }
}
