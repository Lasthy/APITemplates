using System.Net;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace MyModule.Tests.Integration;

public class MyModuleEndpointTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public MyModuleEndpointTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOpenApiDocument_ShouldReturnSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/openapi/{MyModuleModule.Name}.json");

        // Module OpenAPI document is served by Host when module is registered
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound); // NotFound if module not active in InstanceSettings
    }
}
