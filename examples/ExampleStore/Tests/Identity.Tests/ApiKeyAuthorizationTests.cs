using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Identity.Controllers;
using Identity.Entities;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Identity.Controllers.ApiKeysController;
using static Identity.Controllers.AuthController;
using static Identity.Controllers.UsersController;

namespace Identity.Tests;

public class ApiKeyAuthorizationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiKeyAuthorizationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new UlidJsonConverter());
    }

    private async Task<UserDto> RegisterUserAsync(string username, string email, string profileName)
    {
        var password = "SecurePassword123!";
        var registerClient = profileName == IdentityProfiles.Admin
            ? _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System)
            : _client;

        var regResponse = await registerClient.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: email,
            UserName: username,
            Password: password,
            ProfileName: profileName));

        Assert.True(regResponse.IsSuccessStatusCode, $"Failed to register user: {await regResponse.Content.ReadAsStringAsync()}");
        var user = await regResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        return user;
    }

    private async Task<(HttpClient Client, UserDto User)> CreateAuthenticatedUserClientAsync(string username, string email, string profileName)
    {
        var user = await RegisterUserAsync(username, email, profileName);
        var password = "SecurePassword123!";

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: email,
            Password: password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthTokenDto>(_jsonOptions);
        Assert.NotNull(token);

        var authedClient = _factory.CreateAuthenticatedClient(token.AccessToken);
        return (authedClient, user);
    }

    [Fact]
    public async Task Buyer_CannotCreateApiKey_ReturnsForbidden()
    {
        // Buyers have CanManageApiKeys => false on their profile
        var (client, _) = await CreateAuthenticatedUserClientAsync("buyer_keys_test", "buyer_keys_test@example.com", IdentityProfiles.Buyer);

        var request = new CreateApiKeyRequest(Name: "buyer-key");
        var response = await client.PostAsJsonAsync("/api/v1/identity/api-keys", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateApiKey_ReturnsCreatedWithScopes()
    {
        // Admin profile has CanManageApiKeys => true
        var (client, _) = await CreateAuthenticatedUserClientAsync("admin_keys_test", "admin_keys_test@example.com", IdentityProfiles.Admin);

        var request = new CreateApiKeyRequest(
            Name: "admin-ci-key",
            ExpiresAt: DateTime.UtcNow.AddDays(30),
            Scopes: ["catalog:read", "orders:manage"]);

        var response = await client.PostAsJsonAsync("/api/v1/identity/api-keys", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdKey = await response.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(createdKey);
        Assert.StartsWith("mwa_", createdKey.Key);
        Assert.Equal("admin-ci-key", createdKey.Name);
        Assert.NotNull(createdKey.Scopes);
        Assert.Contains("catalog:read", createdKey.Scopes);
        Assert.Contains("orders:manage", createdKey.Scopes);
    }

    [Fact]
    public async Task SystemUser_CanCreateApiKey_ReturnsCreated()
    {
        // Register a user to own the key
        var buyerUser = await RegisterUserAsync("system_target_user", "system_target_user@example.com", IdentityProfiles.Buyer);

        // Authenticate as System super-user
        var systemUserId = Ulid.NewUlid();
        var systemClient = _factory.CreateAuthenticatedClient(systemUserId, IdentityRoles.System);

        // System creates an API key on behalf of target user
        var request = new CreateApiKeyRequest(Name: "system-service-key", UserId: buyerUser.Id);
        var response = await systemClient.PostAsJsonAsync("/api/v1/identity/api-keys", request, _jsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdKey = await response.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(createdKey);
        Assert.StartsWith("mwa_", createdKey.Key);
        Assert.Equal("system-service-key", createdKey.Name);
        Assert.Equal(buyerUser.Id, createdKey.UserId);
    }

    [Fact]
    public async Task ApiKey_CannotCreateApiKey_AntiRecursion_ReturnsForbidden()
    {
        // 1. Create an API key using an Admin user
        var (adminClient, _) = await CreateAuthenticatedUserClientAsync("admin_recurs_test", "admin_recurs_test@example.com", IdentityProfiles.Admin);

        var createKeyResponse = await adminClient.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: "parent-key"));
        Assert.Equal(HttpStatusCode.Created, createKeyResponse.StatusCode);
        var createdKey = await createKeyResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(createdKey);

        // 2. Authenticate using this API key
        var apiKeyClient = _factory.CreateApiKeyClient(createdKey.Key);

        // 3. Attempt to create another API key using the API key -> Must be rejected (Anti-recursion)
        var attemptResponse = await apiKeyClient.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: "child-key"));
        Assert.Equal(HttpStatusCode.Forbidden, attemptResponse.StatusCode);
    }

    [Fact]
    public async Task ApiKey_CanAuthenticateMeEndpoint()
    {
        // 1. Create an API key
        var (adminClient, _) = await CreateAuthenticatedUserClientAsync("admin_me_test", "admin_me_test@example.com", IdentityProfiles.Admin);
        var createKeyResponse = await adminClient.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: "me-test-key"));
        Assert.Equal(HttpStatusCode.Created, createKeyResponse.StatusCode);
        var createdKey = await createKeyResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(createdKey);

        // 2. Call /me with the API key
        var apiKeyClient = _factory.CreateApiKeyClient(createdKey.Key);
        var meResponse = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meDto = await meResponse.Content.ReadFromJsonAsync<UserDetailDto>(_jsonOptions);
        Assert.NotNull(meDto);
        Assert.Equal("admin_me_test@example.com", meDto.Email);
    }

    [Fact]
    public async Task UserWithExplicitClaim_CanCreateApiKey()
    {
        // 1. Register a buyer (who by default cannot create keys)
        var username = "buyer_claim_test";
        var email = "buyer_claim_test@example.com";
        var userDto = await RegisterUserAsync(username, email, IdentityProfiles.Buyer);

        // 2. Grant the explicit can_manage_api_keys claim directly to the user
        await _factory.ExecuteScopeAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userDto.Id.ToString());
            Assert.NotNull(user);
            var result = await userManager.AddClaimAsync(user, new Claim(IdentityClaimTypes.CanManageApiKeys, "true"));
            Assert.True(result.Succeeded);
        });

        // 3. Login as this buyer
        var password = "SecurePassword123!";
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: email,
            Password: password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthTokenDto>(_jsonOptions);
        Assert.NotNull(token);

        var authedClient = _factory.CreateAuthenticatedClient(token.AccessToken);

        // 4. Now attempt to create an API key -> Should succeed because of explicit claim
        var response = await authedClient.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: "explicit-claim-key"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ActiveKeyQuota_Exceeded_ReturnsBadRequest()
    {
        // Default MaxKeysPerUser is 5
        var (client, _) = await CreateAuthenticatedUserClientAsync("admin_quota_test", "admin_quota_test@example.com", IdentityProfiles.Admin);

        for (int i = 1; i <= 5; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: $"quota-key-{i}"));
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        // 6th key should fail quota
        var overflowResponse = await client.PostAsJsonAsync("/api/v1/identity/api-keys", new CreateApiKeyRequest(Name: "quota-key-6"));
        Assert.Equal(HttpStatusCode.BadRequest, overflowResponse.StatusCode);
        var content = await overflowResponse.Content.ReadAsStringAsync();
        Assert.Contains("maximum number of active API keys", content);
    }

    [Fact]
    public async Task ExpirationInPast_ReturnsBadRequest()
    {
        var (client, _) = await CreateAuthenticatedUserClientAsync("admin_exp_test", "admin_exp_test@example.com", IdentityProfiles.Admin);

        var request = new CreateApiKeyRequest(
            Name: "expired-key",
            ExpiresAt: DateTime.UtcNow.AddMinutes(-10));

        var response = await client.PostAsJsonAsync("/api/v1/identity/api-keys", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("future", content);
    }
}
