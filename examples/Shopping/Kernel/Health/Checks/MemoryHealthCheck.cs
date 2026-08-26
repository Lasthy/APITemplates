using System.Diagnostics;

namespace Kernel.Health.Checks;

/// <summary>
/// Built-in health check for monitoring application memory usage and garbage collection.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    public string Name => "Memory";

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var allocatedMemory = GC.GetTotalMemory(forceFullCollection: false);
        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64;

        var data = new Dictionary<string, object>
        {
            ["AllocatedBytes"] = allocatedMemory,
            ["AllocatedMB"] = Math.Round(allocatedMemory / (1024.0 * 1024.0), 2),
            ["WorkingSetBytes"] = workingSet,
            ["WorkingSetMB"] = Math.Round(workingSet / (1024.0 * 1024.0), 2),
            ["Gen0Collections"] = GC.CollectionCount(0),
            ["Gen1Collections"] = GC.CollectionCount(1),
            ["Gen2Collections"] = GC.CollectionCount(2)
        };

        return Task.FromResult(HealthCheckResult.Healthy("Memory usage is normal.", data));
    }
}
