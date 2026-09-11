using Kernel.Health;
using Kernel.Health.Checks;
using Xunit;

namespace Kernel.Tests.Health;

public class HealthChecksTests
{
    [Fact]
    public void HealthCheckResult_FactoryMethods_ShouldInitializeCorrectly()
    {
        var healthy = HealthCheckResult.Healthy("All systems green");
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
        Assert.Equal("All systems green", healthy.Description);
        Assert.Null(healthy.Exception);

        var degraded = HealthCheckResult.Degraded("High latency");
        Assert.Equal(HealthStatus.Degraded, degraded.Status);

        var ex = new InvalidOperationException("Connection failed");
        var unhealthy = HealthCheckResult.Unhealthy("Db down", ex);
        Assert.Equal(HealthStatus.Unhealthy, unhealthy.Status);
        Assert.Same(ex, unhealthy.Exception);
    }

    [Fact]
    public async Task MemoryHealthCheck_ShouldReturnHealthyWithMetrics()
    {
        var check = new MemoryHealthCheck();
        var result = await check.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("AllocatedBytes"));
        Assert.True(result.Data.ContainsKey("WorkingSetBytes"));
    }

    [Fact]
    public void HealthCheckRegistry_ShouldRegisterAndQueryChecks()
    {
        var registry = new HealthCheckRegistry();

        registry.Register<MemoryHealthCheck>("Host");

        var modules = registry.GetRegistrations();
        Assert.True(modules.ContainsKey("Host"));

        var checks = modules["Host"];
        Assert.Single(checks);
        Assert.Equal(typeof(MemoryHealthCheck), checks[0].ImplementationType);
    }
}
