using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kernel.Configuration;
using Kernel.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Shopping.Tests;

public class RateLimitingTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public RateLimitingTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EndpointOverride_ExceedingLimit_Returns429TooManyRequests()
    {
        var client = _factory.CreateClient();
        var clientIp = "10.0.0.1";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);

        // auth/login has a limit of 5 requests per window in appsettings.RateLimiting.json
        HttpResponseMessage lastResponse = null!;
        for (int i = 0; i < 5; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
            {
                Email = "test@example.com",
                Password = "WrongPassword123!"
            });
            // First 5 requests should not be rate limited (they return 401 Unauthorized for bad creds)
            Assert.Equal(HttpStatusCode.Unauthorized, lastResponse.StatusCode);
        }

        // 6th request should exceed the limit of 5 and return 429 Too Many Requests
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        });

        Assert.Equal((HttpStatusCode)429, rateLimitedResponse.StatusCode);

        var body = await rateLimitedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("statusCode").GetInt32());
        Assert.Contains("Too many requests", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DistinctClientIps_ArePartitionedIndependently()
    {
        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.10");

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.20");

        // Client 1 sends 5 requests
        for (int i = 0; i < 5; i++)
        {
            var res = await client1.PostAsJsonAsync("/api/v1/identity/auth/login", new
            {
                Email = "test@example.com",
                Password = "WrongPassword123!"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // Client 1 is now rate limited
        var client1Exceeded = await client1.PostAsJsonAsync("/api/v1/identity/auth/login", new
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        });
        Assert.Equal((HttpStatusCode)429, client1Exceeded.StatusCode);

        // Client 2 (different IP) can still send requests successfully without hitting 429
        var client2Response = await client2.PostAsJsonAsync("/api/v1/identity/auth/login", new
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, client2Response.StatusCode);
    }

    [Fact]
    public async Task DisablingRateLimiting_AllowsRequestsBeyondLimit()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<RateLimitingSettings>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.30");

        try
        {
            // Temporarily disable rate limiting
            settings.SetValue("Enabled", false);

            // Send 10 requests (limit was 5)
            for (int i = 0; i < 10; i++)
            {
                var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
                {
                    Email = "test@example.com",
                    Password = "WrongPassword123!"
                });
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
        finally
        {
            // Restore enabled state
            settings.SetValue("Enabled", true);
        }
    }

    [Fact]
    public async Task DynamicConfiguration_CustomRejectionMessage_IsReflected()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<RateLimitingSettings>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.40");

        var customMsg = "Custom rate limit exceeded message.";
        try
        {
            settings.SetValue("RejectionMessage", customMsg);

            // Exhaust 5 permits
            for (int i = 0; i < 5; i++)
            {
                await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
                {
                    Email = "test@example.com",
                    Password = "WrongPassword123!"
                });
            }

            // 6th request
            var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new
            {
                Email = "test@example.com",
                Password = "WrongPassword123!"
            });

            Assert.Equal((HttpStatusCode)429, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(customMsg, body.GetProperty("message").GetString());
        }
        finally
        {
            settings.SetValue("RejectionMessage", "Too many requests. Please try again later.");
        }
    }

    [Fact]
    public async Task AuthenticatedUser_AppliesAuthenticatedPolicy()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var authClient = _factory.CreateAuthenticatedClient(token);
        authClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.50");

        // Authenticated users hitting a non-override endpoint (/api/v1/kernel/health/live)
        // should easily be able to make multiple requests within the 500 permit limit
        for (int i = 0; i < 15; i++)
        {
            var response = await authClient.GetAsync("/api/v1/kernel/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task DynamicEndpointOverride_CustomLimit_IsEnforced()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<RateLimitingSettings>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.60");

        var originalOverrides = settings.EndpointOverrides;
        try
        {
            // Add custom endpoint override for "health/live" with permit limit of 2
            var updatedOverrides = new Dictionary<string, RateLimitEndpointConfig>(originalOverrides, StringComparer.OrdinalIgnoreCase)
            {
                ["kernel/health/live"] = new RateLimitEndpointConfig { PermitLimit = 2, WindowSeconds = 60, QueueLimit = 0 }
            };

            settings.SetValue("EndpointOverrides", updatedOverrides);

            // First 2 health checks pass
            var resp1 = await client.GetAsync("/api/v1/kernel/health/live");
            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

            var resp2 = await client.GetAsync("/api/v1/kernel/health/live");
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

            // 3rd health check is rate limited
            var resp3 = await client.GetAsync("/api/v1/kernel/health/live");
            Assert.Equal((HttpStatusCode)429, resp3.StatusCode);
        }
        finally
        {
            settings.SetValue("EndpointOverrides", originalOverrides);
        }
    }
}

