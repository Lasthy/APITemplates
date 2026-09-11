using System.Net;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace Kernel.Tests.Host;

public class HostIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HostIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetKernelOpenApi_ShouldReturnSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/kernel.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/kernel/health/live");

        // Basic health check is public and returns 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_Anonymous_ShouldBeUnauthorizedOrForbidden()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/kernel/settings/Host");

        // SettingsController is protected by [Authorize(Roles = Roles.System)]
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSettings_WithSystemRole_ShouldReturnOk()
    {
        var client = _factory.CreateAuthenticatedClient(DefaultId.NewUlid(), Kernel.Constants.Roles.System);

        var response = await client.GetAsync("/api/v1/kernel/settings/Host");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }
}
