using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Contracts.Dtos;
using Identity.Entities;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Shopping.Tests;

public class ApiKeyAuthenticationTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new UlidJsonConverter() }
    };

    public ApiKeyAuthenticationTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetClientTokenAsync(HttpClient client, string? email = null)
    {
        email ??= $"client_{Guid.NewGuid():N}@shopping.local";
        var username = $"user_{Guid.NewGuid():N}"[..15];

        await client.PostAsJsonAsync("/api/v1/identity/users/register", new
        {
            Email = email,
            UserName = username,
            Password = "Password123!",
            ProfileName = "Client"
        }, _jsonOptions);

        return await _factory.LoginAsync(client, email, "Password123!");
    }

    [Fact]
    public async Task CreateApiKey_ReturnsPlaintextSecretOnce_AndStoresOnlyHashInDatabase()
    {
        var anonymousClient = _factory.CreateClient();
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var authenticatedClient = _factory.CreateAuthenticatedClient(clientToken);

        // 1. Create API key
        var createResp = await authenticatedClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "CLI Deployment Key"
        }, _jsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal("CLI Deployment Key", created.Name);
        Assert.StartsWith("mwa_", created.Key);
        Assert.Equal(created.Key[..8], created.KeyPrefix);

        // 2. Verify Database storage: only SHA-256 hash is stored, never plaintext key
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var apiKeyEntity = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == created.Id);

        Assert.NotNull(apiKeyEntity);
        Assert.NotEqual(created.Key, apiKeyEntity.KeyHash);
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(created.Key))).ToLowerInvariant();
        Assert.Equal(computedHash, apiKeyEntity.KeyHash);
    }

    [Fact]
    public async Task AuthenticateWithApiKey_AccessesAuthorizedEndpoints_AndUpdatesLastUsedAt()
    {
        var anonymousClient = _factory.CreateClient();
        var email = $"client_{Guid.NewGuid():N}@shopping.local";
        var clientToken = await GetClientTokenAsync(anonymousClient, email);
        var authenticatedClient = _factory.CreateAuthenticatedClient(clientToken);

        // 1. Create API key
        var createResp = await authenticatedClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Test Access Key"
        }, _jsonOptions);
        var created = await createResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(created);

        // 2. Make authenticated call with X-API-Key header to /me endpoint
        var apiKeyClient = _factory.CreateApiKeyClient(created.Key);
        var meResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var me = await meResp.Content.ReadFromJsonAsync<UserDetailDto>(_jsonOptions);
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);

        // 3. Verify LastUsedAt is updated in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var apiKeyEntity = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == created.Id);
        Assert.NotNull(apiKeyEntity);
        Assert.NotNull(apiKeyEntity.LastUsedAt);
        Assert.True(apiKeyEntity.LastUsedAt.Value > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task AuthenticateWithApiKey_InheritsRoles_AndEnforcesRolePolicies()
    {
        var anonymousClient = _factory.CreateClient();

        // 1. Create Client API key
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var clientAuthClient = _factory.CreateAuthenticatedClient(clientToken);
        var clientKeyResp = await clientAuthClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Client Key"
        }, _jsonOptions);
        var clientKey = await clientKeyResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(clientKey);

        // 2. Create Manager API key
        var systemToken = await _factory.LoginAsync(anonymousClient, "admin@localhost", "Admin123!");
        var systemAuthClient = _factory.CreateAuthenticatedClient(systemToken);
        var managerKeyResp = await systemAuthClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Manager Key"
        }, _jsonOptions);
        var managerKey = await managerKeyResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(managerKey);

        var clientApiKeyClient = _factory.CreateApiKeyClient(clientKey.Key);
        var managerApiKeyClient = _factory.CreateApiKeyClient(managerKey.Key);

        // Client API Key cannot access Manager endpoint (/api/v1/identity/users)
        var clientGetUsersResp = await clientApiKeyClient.GetAsync("/api/v1/identity/users");
        Assert.Equal(HttpStatusCode.Forbidden, clientGetUsersResp.StatusCode);

        // Manager API Key CAN access Manager endpoint (/api/v1/identity/users)
        var managerGetUsersResp = await managerApiKeyClient.GetAsync("/api/v1/identity/users");
        Assert.Equal(HttpStatusCode.OK, managerGetUsersResp.StatusCode);
    }

    [Fact]
    public async Task AuthenticateWithExpiredApiKey_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var authenticatedClient = _factory.CreateAuthenticatedClient(clientToken);

        // Create API key that expires in the past
        var createResp = await authenticatedClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Expired Key",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        }, _jsonOptions);
        var created = await createResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(created);

        // Attempt access
        var apiKeyClient = _factory.CreateApiKeyClient(created.Key);
        var meResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, meResp.StatusCode);
    }

    [Fact]
    public async Task AuthenticateWithRevokedApiKey_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var authenticatedClient = _factory.CreateAuthenticatedClient(clientToken);

        // 1. Create API key
        var createResp = await authenticatedClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Key To Revoke"
        }, _jsonOptions);
        var created = await createResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(created);

        // 2. Verify key works before revocation
        var apiKeyClient = _factory.CreateApiKeyClient(created.Key);
        var preRevokeResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.OK, preRevokeResp.StatusCode);

        // 3. Revoke key
        var revokeResp = await authenticatedClient.DeleteAsync($"/api/v1/identity/api-keys/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);

        // 4. Verify key fails after revocation
        var postRevokeResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, postRevokeResp.StatusCode);
    }

    [Fact]
    public async Task AuthenticateWithBannedUserApiKey_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        // 1. Register a new user
        var email = $"ban_test_{Guid.NewGuid():N}@shopping.local";
        var username = $"banuser_{Guid.NewGuid():N}"[..15];
        var regResp = await anonymousClient.PostAsJsonAsync("/api/v1/identity/users/register", new
        {
            Email = email,
            UserName = username,
            Password = "Password123!",
            ProfileName = "Client"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        var user = await regResp.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);

        // 2. Login and create API key
        var userToken = await _factory.LoginAsync(anonymousClient, email, "Password123!");
        var userAuthClient = _factory.CreateAuthenticatedClient(userToken);
        var createKeyResp = await userAuthClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Banned User Key"
        }, _jsonOptions);
        var key = await createKeyResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(key);

        var apiKeyClient = _factory.CreateApiKeyClient(key.Key);
        var beforeBanResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.OK, beforeBanResp.StatusCode);

        // 3. Ban user as System superuser
        var systemToken = await _factory.LoginAsync(anonymousClient, "admin@localhost", "Admin123!");
        var systemAuthClient = _factory.CreateAuthenticatedClient(systemToken);
        var banResp = await systemAuthClient.PostAsJsonAsync($"/api/v1/identity/users/{user.Id}/ban", new
        {
            Reason = "Violation of terms"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, banResp.StatusCode);

        // 4. API Key access should now be rejected (banned user)
        var afterBanResp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterBanResp.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_ReturnsUnauthorized()
    {
        var apiKeyClient = _factory.CreateApiKeyClient("mwa_invalidnonexistentkey1234567890");
        var resp = await apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ListAndGetApiKeys_EnforcesUserIsolation_AndAdminVisibility()
    {
        var anonymousClient = _factory.CreateClient();

        // 1. Client creates an API key
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var clientAuthClient = _factory.CreateAuthenticatedClient(clientToken);
        var clientKeyResp = await clientAuthClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "User Isolated Key"
        }, _jsonOptions);
        var clientKey = await clientKeyResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(clientKey);

        // 2. Client lists keys -> sees their key
        var clientListResp = await clientAuthClient.GetAsync("/api/v1/identity/api-keys");
        Assert.Equal(HttpStatusCode.OK, clientListResp.StatusCode);
        var clientKeys = await clientListResp.Content.ReadFromJsonAsync<List<ApiKeyDto>>(_jsonOptions);
        Assert.NotNull(clientKeys);
        Assert.Contains(clientKeys, k => k.Id == clientKey.Id);

        // 3. Client gets specific key by ID
        var getKeyResp = await clientAuthClient.GetAsync($"/api/v1/identity/api-keys/{clientKey.Id}");
        Assert.Equal(HttpStatusCode.OK, getKeyResp.StatusCode);
        var fetchedKey = await getKeyResp.Content.ReadFromJsonAsync<ApiKeyDto>(_jsonOptions);
        Assert.NotNull(fetchedKey);
        Assert.Equal("User Isolated Key", fetchedKey.Name);
        Assert.True(fetchedKey.IsActive);
        Assert.False(fetchedKey.IsRevoked);

        // 4. System superuser can list keys filtered by user ID
        var systemToken = await _factory.LoginAsync(anonymousClient, "admin@localhost", "Admin123!");
        var systemAuthClient = _factory.CreateAuthenticatedClient(systemToken);
        var systemListResp = await systemAuthClient.GetAsync($"/api/v1/identity/api-keys?userId={clientKey.UserId}");
        Assert.Equal(HttpStatusCode.OK, systemListResp.StatusCode);
        var systemKeys = await systemListResp.Content.ReadFromJsonAsync<List<ApiKeyDto>>(_jsonOptions);
        Assert.NotNull(systemKeys);
        Assert.Contains(systemKeys, k => k.Id == clientKey.Id);
    }

    [Fact]
    public async Task ConcurrentJwtAndApiKeyRequests_BothAuthenticateSuccessfully()
    {
        var anonymousClient = _factory.CreateClient();
        var clientToken = await GetClientTokenAsync(anonymousClient);
        var jwtClient = _factory.CreateAuthenticatedClient(clientToken);

        // Create API key
        var createKeyResp = await jwtClient.PostAsJsonAsync("/api/v1/identity/api-keys", new
        {
            Name = "Concurrent Key"
        }, _jsonOptions);
        var key = await createKeyResp.Content.ReadFromJsonAsync<ApiKeyCreatedDto>(_jsonOptions);
        Assert.NotNull(key);

        var apiKeyClient = _factory.CreateApiKeyClient(key.Key);

        // Run concurrent requests with JWT and API Key
        var jwtTask1 = jwtClient.GetAsync("/api/v1/identity/auth/me");
        var apiKeyTask1 = apiKeyClient.GetAsync("/api/v1/identity/auth/me");
        var jwtTask2 = jwtClient.GetAsync("/api/v1/identity/api-keys");
        var apiKeyTask2 = apiKeyClient.GetAsync("/api/v1/identity/api-keys");

        await Task.WhenAll(jwtTask1, apiKeyTask1, jwtTask2, apiKeyTask2);

        Assert.Equal(HttpStatusCode.OK, (await jwtTask1).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await apiKeyTask1).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jwtTask2).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await apiKeyTask2).StatusCode);
    }
}
