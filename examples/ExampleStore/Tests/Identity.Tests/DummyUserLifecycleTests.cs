using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Identity.Controllers;
using Identity.Entities;
using Identity.Persistence;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Identity.Controllers.AuthController;
using static Identity.Controllers.UsersController;

namespace Identity.Tests;

public class DummyUserLifecycleTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public DummyUserLifecycleTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new UlidJsonConverter());
    }

    [Fact]
    public async Task CreateDummyUser_ReturnsCredentialsAndTokens()
    {
        // 1. Create a source user (Buyer)
        var sourceUserEmail = $"source_{Ulid.NewUlid()}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: sourceUserEmail,
            UserName: $"source_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer));
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);
        var sourceUser = await regResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser);

        // 2. Admin creates dummy clone of the source user
        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System);
        await adminClient.DeleteAsync("/api/v1/identity/users/dummy/expired");

        var dummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 60));

        Assert.Equal(HttpStatusCode.OK, dummyResponse.StatusCode);
        var dummyDto = await dummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);

        Assert.NotNull(dummyDto);
        Assert.False(string.IsNullOrWhiteSpace(dummyDto.UserName));
        Assert.False(string.IsNullOrWhiteSpace(dummyDto.Email));
        Assert.False(string.IsNullOrWhiteSpace(dummyDto.Password));
        Assert.False(dummyDto.IsRecycled);
        Assert.True(dummyDto.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(dummyDto.Tokens);
        Assert.False(string.IsNullOrWhiteSpace(dummyDto.Tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(dummyDto.Tokens.RefreshToken));
    }

    [Fact]
    public async Task DummyUser_CanAuthenticate_UsingReturnedCredentials()
    {
        // 1. Create source user
        var sourceUserEmail = $"source_{Ulid.NewUlid()}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: sourceUserEmail,
            UserName: $"source_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer));
        var sourceUser = await regResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser);

        // 2. Admin creates dummy user
        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System);
        var dummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 120));
        var dummyDto = await dummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(dummyDto);

        // 3. Login using returned email and password
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: dummyDto.Email,
            Password: dummyDto.Password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthTokenDto>(_jsonOptions);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        // 4. Also test login using returned username and password
        var usernameLoginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: dummyDto.UserName,
            Password: dummyDto.Password));
        Assert.Equal(HttpStatusCode.OK, usernameLoginResponse.StatusCode);

        // 5. Use access token to fetch /me
        using var dummyClient = _factory.CreateClient();
        dummyClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var meResponse = await dummyClient.GetAsync("/api/v1/identity/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meDto = await meResponse.Content.ReadFromJsonAsync<UserDetailDto>(_jsonOptions);
        Assert.NotNull(meDto);
        Assert.True(meDto.IsDummy);
        Assert.NotNull(meDto.DummyExpiresAt);
        Assert.Equal(IdentityProfiles.Buyer, meDto.ProfileName);
    }

    [Fact]
    public async Task ExpiredDummyUser_CannotAuthenticate_OrRefresh()
    {
        // 1. Create source user
        var sourceUserEmail = $"source_{Ulid.NewUlid()}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: sourceUserEmail,
            UserName: $"source_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer));
        var sourceUser = await regResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser);

        // 2. Admin creates dummy user
        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System);
        var dummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 60));
        var dummyDto = await dummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(dummyDto);

        // 3. Manually expire the dummy user in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var dbUser = await dbContext.Users.FindAsync(dummyDto.Id);
            Assert.NotNull(dbUser);
            dbUser.DummyExpiresAt = DateTime.UtcNow.AddMinutes(-5); // Expired 5 minutes ago
            await dbContext.SaveChangesAsync();
        }

        // 4. Try logging in as the expired dummy user -> should fail with 400 or 401
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: dummyDto.Email,
            Password: dummyDto.Password));
        Assert.False(loginResponse.IsSuccessStatusCode);
        var errorContent = await loginResponse.Content.ReadAsStringAsync();
        Assert.Contains("Dummy account has expired", errorContent);

        // 5. Try refreshing token for the expired dummy user -> should fail
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new RefreshTokenRequest(
            Token: dummyDto.Tokens.RefreshToken));
        Assert.False(refreshResponse.IsSuccessStatusCode);
        var refreshError = await refreshResponse.Content.ReadAsStringAsync();
        Assert.Contains("Dummy account has expired", refreshError);
    }

    [Fact]
    public async Task CreateDummyUser_RecyclesExistingExpiredDummyUser()
    {
        // 1. Create source user 1 and source user 2
        var sourceUser1Response = await _client.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: $"source1_{Ulid.NewUlid()}@example.com",
            UserName: $"src1_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer));
        var sourceUser1 = await sourceUser1Response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser1);

        var adminUserId = Ulid.NewUlid();
        var sourceUser2Response = await _factory.CreateAuthenticatedClient(adminUserId, IdentityRoles.System)
            .PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
                Email: $"source2_{Ulid.NewUlid()}@example.com",
                UserName: $"src2_{Ulid.NewUlid()}",
                Password: "SecurePassword123!",
                ProfileName: IdentityProfiles.Admin));
        var sourceUser2 = await sourceUser2Response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser2);

        // 2. Admin creates dummy user cloning sourceUser1
        var adminClient = _factory.CreateAuthenticatedClient(adminUserId, IdentityRoles.System);
        await adminClient.DeleteAsync("/api/v1/identity/users/dummy/expired");

        var initialDummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser1.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 60));
        var initialDummy = await initialDummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(initialDummy);
        Assert.False(initialDummy.IsRecycled);

        // 3. Mark the dummy user as expired in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var dbUser = await dbContext.Users.FindAsync(initialDummy.Id);
            Assert.NotNull(dbUser);
            dbUser.DummyExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await dbContext.SaveChangesAsync();
        }

        // 4. Request a new dummy user cloning sourceUser2
        var recycledDummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser2.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 180));

        Assert.Equal(HttpStatusCode.OK, recycledDummyResponse.StatusCode);
        var recycledDummy = await recycledDummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(recycledDummy);

        // 5. Verify it was recycled!
        Assert.True(recycledDummy.IsRecycled);
        Assert.Equal(initialDummy.Id, recycledDummy.Id); // Same user entity ID reused!
        Assert.NotEqual(initialDummy.Password, recycledDummy.Password); // New password issued
        Assert.True(recycledDummy.ExpiresAt > DateTime.UtcNow); // Expiration extended

        // 6. Verify old password no longer works
        var oldLoginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: recycledDummy.Email,
            Password: initialDummy.Password));
        Assert.False(oldLoginResponse.IsSuccessStatusCode);

        // 7. Verify new password works
        var newLoginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: recycledDummy.Email,
            Password: recycledDummy.Password));
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);

        // 8. Verify the recycled dummy now has sourceUser2's Admin profile
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var user = await dbContext.Users.FindAsync(recycledDummy.Id);
            Assert.NotNull(user);
            Assert.Equal(IdentityProfiles.Admin, user.ProfileName);
        }
    }

    [Fact]
    public async Task PurgeExpiredDummies_DeletesOnlyExpiredDummies()
    {
        // 1. Create source user
        var sourceUserResponse = await _client.PostAsJsonAsync("/api/v1/identity/users/register", new RegisterRequest(
            Email: $"source_purge_{Ulid.NewUlid()}@example.com",
            UserName: $"purge_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer));
        var sourceUser = await sourceUserResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(sourceUser);

        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System);

        // 2. Create two dummy users: one active, one to expire
        var activeDummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 600));
        var activeDummy = await activeDummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(activeDummy);

        var expiredDummyResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/identity/users/{sourceUser.Id}/dummy",
            new CreateDummyUserRequest(LifetimeMinutes: 60));
        var expiredDummy = await expiredDummyResponse.Content.ReadFromJsonAsync<DummyUserDto>(_jsonOptions);
        Assert.NotNull(expiredDummy);

        // 3. Set one to be expired in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var userToExpire = await dbContext.Users.FindAsync(expiredDummy.Id);
            Assert.NotNull(userToExpire);
            userToExpire.DummyExpiresAt = DateTime.UtcNow.AddMinutes(-30);
            await dbContext.SaveChangesAsync();
        }

        // 4. Call Purge endpoint
        var purgeResponse = await adminClient.DeleteAsync("/api/v1/identity/users/dummy/expired");
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);

        // 5. Verify expired dummy is deleted, but active dummy remains
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var deletedUser = await dbContext.Users.FindAsync(expiredDummy.Id);
            Assert.Null(deletedUser);

            var remainingUser = await dbContext.Users.FindAsync(activeDummy.Id);
            Assert.NotNull(remainingUser);
        }
    }
}
