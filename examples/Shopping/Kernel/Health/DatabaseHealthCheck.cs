using Kernel.Persistence;

namespace Kernel.Health;

/// <summary>
/// Health check that verifies database connectivity using <see cref="KernelDbContext"/>.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly KernelDbContext _dbContext;

    public string Name => "Database";

    public DatabaseHealthCheck(KernelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["Provider"] = _dbContext.Database.ProviderName ?? "Unknown"
            };

            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is established.", data);
            }

            return HealthCheckResult.Unhealthy("Database connection could not be established.", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed with an exception.", exception: ex);
        }
    }
}
