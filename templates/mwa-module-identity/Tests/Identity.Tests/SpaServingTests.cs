using System.Net;
using Kernel.Configuration;
using Kernel.Json;
using Kernel.Spa;
using Kernel.Spa.Extensions;
using Kernel.Testing.Assertions;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class SpaServingTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly IJsonSerializer _jsonSerializer = new JsonSerializer();

    public SpaServingTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public void BasePathNormalization_ShouldNormalizeCorrectly()
    {
        // Assert
        Assert.Equal("/", SpaAppOptions.NormalizeBasePath("/"));
        Assert.Equal("/", SpaAppOptions.NormalizeBasePath(""));
        Assert.Equal("/", SpaAppOptions.NormalizeBasePath(null));
        Assert.Equal("/", SpaAppOptions.NormalizeBasePath("   "));
        Assert.Equal("/auth", SpaAppOptions.NormalizeBasePath("auth"));
        Assert.Equal("/auth", SpaAppOptions.NormalizeBasePath("/auth"));
        Assert.Equal("/auth", SpaAppOptions.NormalizeBasePath("/auth/"));
        Assert.Equal("/auth/admin", SpaAppOptions.NormalizeBasePath("auth/admin"));
        Assert.Equal("/auth/admin", SpaAppOptions.NormalizeBasePath("/auth/admin/"));
    }

    [Fact]
    public void SpaRegistry_ShouldRegisterAndOrderAppsByPrefixLengthDescending()
    {
        // Arrange
        var registry = new SpaRegistry();

        var hostWeb = new SpaAppOptions
        {
            Name = "Host.Web",
            Owner = "Host",
            BasePath = "/",
            DevServerUrl = "http://localhost:5173"
        };

        var identityAuth = new SpaAppOptions
        {
            Name = "Identity.Auth",
            Owner = "Identity",
            BasePath = "/auth",
            DevServerUrl = "http://localhost:5174"
        };

        var identityAdmin = new SpaAppOptions
        {
            Name = "Identity.Admin",
            Owner = "Identity",
            BasePath = "/auth/admin",
            DevServerUrl = "http://localhost:5175"
        };

        var ordersPortal = new SpaAppOptions
        {
            Name = "Orders.Portal",
            Owner = "Orders",
            BasePath = "/orders/portal",
            DevServerUrl = "http://localhost:5176"
        };

        // Act - register in random order
        registry.RegisterApp(hostWeb);
        registry.RegisterApp(identityAuth);
        registry.RegisterApp(identityAdmin);
        registry.RegisterApp(ordersPortal);

        // Assert
        registry.ShouldHaveAppCount(4);
        registry.ShouldHaveAppWithBasePath("Identity.Admin", "/auth/admin");
        registry.ShouldHaveAppWithBasePath("Identity.Auth", "/auth");

        // Verify ordering: deepest/longest prefix first
        registry.ShouldHaveEvaluationOrder(
            "Orders.Portal",    // length: 14 ("/orders/portal")
            "Identity.Admin",   // length: 11 ("/auth/admin")
            "Identity.Auth",    // length: 5  ("/auth")
            "Host.Web"          // length: 1  ("/")
        );

        // Query by owner
        var identityApps = registry.GetAppsByOwner("Identity").Select(a => a.Name).ToList();
        Assert.Equal(2, identityApps.Count);
        Assert.Contains("Identity.Auth", identityApps);
        Assert.Contains("Identity.Admin", identityApps);
    }

    [Fact]
    public void SpaRegistry_DisabledApp_ShouldNotBeInEvaluationOrder()
    {
        // Arrange
        var registry = new SpaRegistry();

        registry.RegisterApp(new SpaAppOptions
        {
            Name = "Host.Web",
            Owner = "Host",
            BasePath = "/",
            Enabled = true
        });

        registry.RegisterApp(new SpaAppOptions
        {
            Name = "Identity.Auth",
            Owner = "Identity",
            BasePath = "/auth",
            Enabled = false // disabled
        });

        // Act & Assert
        registry.ShouldHaveEvaluationOrder("Host.Web");
        Assert.Equal(2, registry.Apps.Count); // Still in registry
    }

    [Fact]
    public void SpaSettings_Defaults_ShouldHaveExcludedPrefixesAndDefaults()
    {
        // Arrange & Act
        var settings = new SpaSettings(_jsonSerializer);

        // Assert
        settings.ShouldBeEnabled();
        Assert.Equal(30, settings.ProxyTimeoutSeconds);
        settings.ShouldExcludePrefix("/api");
        settings.ShouldExcludePrefix("/openapi");
        settings.ShouldExcludePrefix("/scalar");
        settings.ShouldExcludePrefix("/docs");
        settings.ShouldExcludePrefix("/hubs");
        settings.ShouldExcludePrefix("/health");
        settings.ShouldExcludePrefix("/files");
    }

    [Fact]
    public void SpaAppOptions_DevServerCommandAndAutoStart_ShouldSetCorrectly()
    {
        // Arrange & Act
        var app = new SpaAppOptions
        {
            Name = "Web",
            BasePath = "/",
            DevServerCommand = "pnpm run dev",
            AutoStartDevServer = true
        };

        // Assert
        app.ShouldHaveDevServerCommand("pnpm run dev");
        app.ShouldAutoStartDevServer(true);
    }

    [Fact]
    public async Task SpaProcessManager_WhenAutoStartDisabled_ShouldNotStart()
    {
        // Arrange
        using var manager = new SpaProcessManager();
        var app = new SpaAppOptions
        {
            Name = "Web",
            BasePath = "/",
            DevServerCommand = "npm run dev",
            AutoStartDevServer = false
        };

        // Act & Assert (should complete immediately without attempting to launch)
        await manager.StartDevServerAsync(app, Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task SpaProcessManager_WhenSourceDirectoryMissing_ShouldGracefullySkip()
    {
        // Arrange
        using var manager = new SpaProcessManager();
        var app = new SpaAppOptions
        {
            Name = "Web",
            BasePath = "/",
            SourcePath = "NonExistentDirectoryForTestingXYZ",
            DevServerCommand = "npm run dev",
            AutoStartDevServer = true
        };

        // Act & Assert (should not throw)
        await manager.StartDevServerAsync(app, Directory.GetCurrentDirectory());
    }

    [Fact]
    public void SpaHtmlTemplates_DevServerOffline_ShouldRenderDiagnosticInfo()
    {
        // Arrange
        var app = new SpaAppOptions
        {
            Name = "Identity.Admin",
            Owner = "Identity",
            BasePath = "/auth/admin",
            DevServerUrl = "http://localhost:5175",
            SourcePath = "Modules/Identity/ClientApp/admin"
        };

        // Act
        var html = SpaHtmlTemplates.GetDevServerOfflineHtml(app, "Connection refused");

        // Assert
        Assert.Contains("SPA Dev Server Offline", html);
        Assert.Contains("Identity.Admin", html);
        Assert.Contains("Identity", html);
        Assert.Contains("/auth/admin", html);
        Assert.Contains("http://localhost:5175", html);
        Assert.Contains("cd Modules/Identity/ClientApp/admin && npm run dev", html);
        Assert.Contains("Connection refused", html);
    }

    [Fact]
    public void SpaHtmlTemplates_MissingDistFolder_ShouldRenderBuildInstructions()
    {
        // Arrange
        var app = new SpaAppOptions
        {
            Name = "Host.Web",
            Owner = "Host",
            BasePath = "/",
            DistPath = "ClientApp/dist",
            SourcePath = "ClientApp"
        };

        // Act
        var html = SpaHtmlTemplates.GetMissingDistFolderHtml(app);

        // Assert
        Assert.Contains("SPA Compiled Assets Not Found", html);
        Assert.Contains("Host.Web", html);
        Assert.Contains("ClientApp/dist", html);
        Assert.Contains("npm run build", html);
    }

    [Fact]
    public void ModuleSpa_RegistrationExtensions_ShouldRegisterCorrectlyIntoDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSpaServices();

        // Act
        services.AddModuleSpa("Identity", "Auth", new SpaAppOptions
        {
            BasePath = "/auth",
            DevServerUrl = "http://localhost:5174",
            DistPath = "Modules/Identity/ClientApp/auth/dist"
        });

        services.AddModuleSpa("Identity", "Admin", new SpaAppOptions
        {
            BasePath = "/auth/admin",
            DevServerUrl = "http://localhost:5175",
            DistPath = "Modules/Identity/ClientApp/admin/dist"
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<SpaRegistry>();

        // Assert
        registry.ShouldHaveAppCount(2);
        registry.ShouldHaveAppWithBasePath("Identity.Auth", "/auth");
        registry.ShouldHaveAppWithBasePath("Identity.Admin", "/auth/admin");
        registry.ShouldHaveEvaluationOrder("Identity.Admin", "Identity.Auth");
    }

    [Fact]
    public async Task ApiRoute_WhenNotFound_ShouldReturn404_AndNeverReturnSpaHtml()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/nonexistent-route");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SPA Dev Server", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/openapi/nonexistent.json")]
    [InlineData("/scalar/nonexistent")]
    [InlineData("/docs/nonexistent")]
    [InlineData("/hubs/nonexistent")]
    [InlineData("/health/nonexistent")]
    public async Task ExcludedPrefixes_WhenEndpointNotFound_ShouldReturn404_AndNotSpa(string path)
    {
        // Act
        var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductionMode_WhenDistMissing_ShouldReturn503_WithDiagnosticHtml()
    {
        // Default factory environment is "Testing" (non-Development / Production behavior)
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("SPA Static Assets Missing", content);
        Assert.Contains("npm run build", content);
    }

    [Fact]
    public async Task ProductionMode_WhenMissingFileWithExtension_ShouldReturn404_AndNotHtml()
    {
        // Act
        var response = await _client.GetAsync("/assets/bundle-xyz.js");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevServerOffline_WhenAccessed_ShouldReturn502_WithDiagnosticHtml()
    {
        // Using Development environment factory
        var devClient = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        // Act
        var response = await devClient.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("SPA Dev Server Unreachable", content);
        Assert.Contains("Web", content);
        Assert.Contains("http://localhost:5173", content);
    }

    [Fact]
    public async Task ModuleSpa_NestedPrefixMatching_ShouldRouteToCorrectDevServer()
    {
        var devFactory = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var devClient = devFactory.CreateClient();

        // Arrange - register module SPAs dynamically in the host's SpaRegistry
        var registry = devFactory.Services.GetRequiredService<SpaRegistry>();
        registry.RegisterApp(new SpaAppOptions
        {
            Name = "Identity.Auth",
            Owner = "Identity",
            BasePath = "/auth",
            DevServerUrl = "http://localhost:5991"
        });
        registry.RegisterApp(new SpaAppOptions
        {
            Name = "Identity.Admin",
            Owner = "Identity",
            BasePath = "/auth/admin",
            DevServerUrl = "http://localhost:5992"
        });

        // Act 1 - Request nested path: should match Identity.Admin (port 5992)
        var responseAdmin = await devClient.GetAsync("/auth/admin/users");
        var contentAdmin = await responseAdmin.Content.ReadAsStringAsync();

        // Assert 1
        Assert.Equal(HttpStatusCode.BadGateway, responseAdmin.StatusCode);
        Assert.Contains("Identity.Admin", contentAdmin);
        Assert.Contains("http://localhost:5992", contentAdmin);

        // Act 2 - Request parent path: should match Identity.Auth (port 5991)
        var responseAuth = await devClient.GetAsync("/auth/login");
        var contentAuth = await responseAuth.Content.ReadAsStringAsync();

        // Assert 2
        Assert.Equal(HttpStatusCode.BadGateway, responseAuth.StatusCode);
        Assert.Contains("Identity.Auth", contentAuth);
        Assert.Contains("http://localhost:5991", contentAuth);
    }
}
