namespace Kernel.Health;

/// <summary>
/// Defines the contract for an individual health check within a module or the Kernel.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// The unique name of the health check component.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the health check and returns a <see cref="HealthCheckResult"/>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
