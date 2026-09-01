using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning;
using Kernel.Configuration;
using Kernel.Modules;
using Kernel.Testing.Assertions;
using Kernel.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Shopping.Tests;

public class ApiVersioningTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public ApiVersioningTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void VersioningSettings_DefaultValues_AreCorrect()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<VersioningSettings>();

        Assert.NotNull(settings);
        Assert.Equal("Versioning", settings.SectionName);
        Assert.Equal("1.0", settings.DefaultVersion);
        Assert.True(settings.AssumeDefaultVersionWhenUnspecified);
        Assert.True(settings.ReportApiVersions);
        Assert.Equal("X-Api-Version", settings.ApiVersionHeader);
        Assert.Equal("api-version", settings.ApiVersionQueryParam);
        Assert.True(settings.EnableUrlSegmentVersioning);
        Assert.True(settings.EnableHeaderVersioning);
        Assert.True(settings.EnableQueryVersioning);
    }

    [Fact]
    public void VersioningSettings_GetDefaultApiVersion_ParsesCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<VersioningSettings>();

        var defaultApiVersion = settings.GetDefaultApiVersion();
        Assert.Equal(new ApiVersion(1, 0), defaultApiVersion);
    }

    [Fact]
    public void VersioningSettings_CreateApiVersionReader_ReturnsCombinedReader()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<VersioningSettings>();

        var reader = settings.CreateApiVersionReader();
        Assert.NotNull(reader);
    }

    [Fact]
    public async Task UrlSegmentVersioning_V1_ReturnsSuccessAndSupportedVersionsHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/kernel/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.ShouldHaveSupportedApiVersions("1.0");
    }

    [Fact]
    public async Task UrlSegmentVersioning_V1_0_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1.0/kernel/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.ShouldHaveSupportedApiVersions("1.0");
    }

    [Fact]
    public async Task UrlSegmentVersioning_UnsupportedVersion_Returns404NotFound()
    {
        var client = _factory.CreateClient();

        // In URL-segment versioning, route constraint fails for unknown version, returning 404
        var response = await client.GetAsync("/api/v99/kernel/health/live");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QueryOrHeaderVersioning_UnsupportedVersion_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Version", "99.0");

        // When a conflicting/unsupported version is explicitly specified via header or query, Asp.Versioning returns 400 Bad Request
        var response = await client.GetAsync("/api/v1/kernel/health/live?api-version=99.0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_V1_ReturnsUnauthorizedWhenAnonymous()
    {
        var client = _factory.CreateClient();

        // /api/v1/kernel/settings requires System role, so unauthenticated returns 401 Unauthorized
        var response = await client.GetAsync("/api/v1/kernel/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_V1_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var authClient = _factory.CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/api/v1/kernel/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.ShouldHaveSupportedApiVersions("1.0");
    }

    [Fact]
    public void ApiVersioningAssertions_ShouldHaveSupportedApiVersions_ThrowsOnMismatch()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("api-supported-versions", "1.0, 2.0");

        // Passes for supported
        response.ShouldHaveSupportedApiVersions("1.0", "2.0");

        // Fails for unsupported version
        Assert.Throws<ContainsException>(() =>
        {
            response.ShouldHaveSupportedApiVersions("3.0");
        });
    }

    [Fact]
    public void ApiVersioningAssertions_ShouldHaveDeprecatedApiVersions_WorksCorrectly()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("api-deprecated-versions", "0.9");

        response.ShouldHaveDeprecatedApiVersions("0.9");

        Assert.Throws<ContainsException>(() =>
        {
            response.ShouldHaveDeprecatedApiVersions("1.0");
        });
    }

    private static IReadOnlyList<ApiVersion> GetModuleSupportedVersions<T>() where T : IModule => T.SupportedVersions;

    [Fact]
    public void IModule_SupportedVersions_DefaultsToV1()
    {
        var defaultVersions = GetModuleSupportedVersions<Catalog.CatalogModule>();
        Assert.NotNull(defaultVersions);
        Assert.Single(defaultVersions);
        Assert.Equal(new ApiVersion(1, 0), defaultVersions[0]);
    }

    [Fact]
    public void IModule_CustomSupportedVersions_CanBeOverridden()
    {
        var versions = GetModuleSupportedVersions<TestCustomVersionModule>();
        Assert.NotNull(versions);
        Assert.Equal(2, versions.Count);
        Assert.Contains(new ApiVersion(1, 0), versions);
        Assert.Contains(new ApiVersion(2, 0), versions);
    }

    private class TestCustomVersionModule : IModule
    {
        public static string Name => "TestCustom";
        public static IReadOnlyList<ApiVersion> SupportedVersions => [new ApiVersion(1, 0), new ApiVersion(2, 0)];
        public static void Register(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration) { }
        public static void RegisterPolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options) { }
        public static void MapEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app) { }
    }

    [Fact]
    public async Task DynamicSettings_VersioningMutation_IsSupported()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<VersioningSettings>();

        try
        {
            settings.SetValue("DefaultVersion", "2.0");
            Assert.Equal("2.0", settings.DefaultVersion);
            Assert.Equal(new ApiVersion(2, 0), settings.GetDefaultApiVersion());

            settings.SetValue("ApiVersionHeader", "X-Custom-Version");
            Assert.Equal("X-Custom-Version", settings.ApiVersionHeader);

            settings.SetValue("ReportApiVersions", false);
            Assert.False(settings.ReportApiVersions);
        }
        finally
        {
            // Reset to defaults
            settings.SetValue("DefaultVersion", "1.0");
            settings.SetValue("ApiVersionHeader", "X-Api-Version");
            settings.SetValue("ReportApiVersions", true);
        }
    }
}

public class KernelVersioningFixtureTests : Kernel.Testing.Infrastructure.KernelTestBase
{
    [Fact]
    public void KernelTestBase_RegistersVersioningSettingsAndServices()
    {
        var settings = DynamicSettings.GetSection<VersioningSettings>();
        Assert.NotNull(settings);
        Assert.Equal("1.0", settings.DefaultVersion);

        var resolvedSettings = Services.GetRequiredService<VersioningSettings>();
        Assert.Same(settings, resolvedSettings);
    }
}
