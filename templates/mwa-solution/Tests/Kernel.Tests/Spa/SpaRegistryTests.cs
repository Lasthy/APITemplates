using Kernel.Spa;
using Kernel.Testing.Assertions;
using Xunit;

namespace Kernel.Tests.Spa;

public class SpaRegistryTests
{
    [Fact]
    public void RegisterApp_And_GetOrderedApps_ShouldOrderDeapestPathFirst()
    {
        var registry = new SpaRegistry();

        var rootSpa = new SpaAppOptions
        {
            Name = "Storefront",
            Owner = "Host",
            BasePath = "/",
            DevServerUrl = "http://localhost:5173",
            Enabled = true
        };

        var authSpa = new SpaAppOptions
        {
            Name = "AuthPortal",
            Owner = "Identity",
            BasePath = "/auth",
            DevServerUrl = "http://localhost:5174",
            Enabled = true
        };

        var adminAuthSpa = new SpaAppOptions
        {
            Name = "AdminAuthPortal",
            Owner = "Identity",
            BasePath = "/auth/admin",
            DevServerUrl = "http://localhost:5175",
            Enabled = true
        };

        registry.RegisterApp(rootSpa);
        registry.RegisterApp(authSpa);
        registry.RegisterApp(adminAuthSpa);

        registry.ShouldHaveApp("Storefront");
        registry.ShouldHaveApp("AuthPortal");
        registry.ShouldHaveApp("AdminAuthPortal");
        registry.ShouldHaveAppWithBasePath("AdminAuthPortal", "/auth/admin");
        registry.ShouldHaveAppCount(3);

        // Evaluation order MUST be: AdminAuthPortal (/auth/admin, length 11) -> AuthPortal (/auth, length 5) -> Storefront (/, length 1)
        registry.ShouldHaveEvaluationOrder("AdminAuthPortal", "AuthPortal", "Storefront");
    }

    [Fact]
    public void GetAppsByOwner_ShouldFilterCorrectly()
    {
        var registry = new SpaRegistry();

        registry.RegisterApp(new SpaAppOptions { Name = "App1", Owner = "Host", BasePath = "/app1", DevServerUrl = "http://localhost:3000" });
        registry.RegisterApp(new SpaAppOptions { Name = "App2", Owner = "Host", BasePath = "/app2", DevServerUrl = "http://localhost:3001" });
        registry.RegisterApp(new SpaAppOptions { Name = "App3", Owner = "CMS", BasePath = "/cms", DevServerUrl = "http://localhost:3002" });

        var hostApps = registry.GetAppsByOwner("Host").ToList();
        var cmsApps = registry.GetAppsByOwner("CMS").ToList();

        Assert.Equal(2, hostApps.Count);
        Assert.Single(cmsApps);
        Assert.Equal("App3", cmsApps[0].Name);
    }
}
