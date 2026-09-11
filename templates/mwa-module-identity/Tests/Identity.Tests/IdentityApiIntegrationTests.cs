using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Controllers;
using static Identity.Controllers.AuthController;
using static Identity.Controllers.UsersController;
using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace Identity.Tests;

public class IdentityApiIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public IdentityApiIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new UlidJsonConverter());
    }

    [Fact]
    public async Task RegisterUser_ViaHttp_ReturnsCreated()
    {
        var registerRequest = new RegisterRequest(
            Email: "user_http@example.com",
            UserName: "user_http",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.User);

        var response = await _client.PostAsJsonAsync("/api/v1/identity/users/register", registerRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        Assert.Equal("user_http@example.com", user.Email);
        Assert.Equal(IdentityProfiles.User, user.ProfileName);
    }

    [Fact]
    public async Task RegisterSystem_Anonymous_ReturnsForbidden()
    {
        var registerRequest = new RegisterRequest(
            Email: "hacker_admin@example.com",
            UserName: "hacker_admin",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.System);

        var response = await _client.PostAsJsonAsync("/api/v1/identity/users/register", registerRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSystem_AsSystem_ReturnsCreated()
    {
        var adminUserId = Ulid.NewUlid();
        var adminClient = _factory.CreateAuthenticatedClient(adminUserId, IdentityRoles.System);

        var registerRequest = new RegisterRequest(
            Email: "new_admin@example.com",
            UserName: "new_admin",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.System);

        var response = await adminClient.PostAsJsonAsync("/api/v1/identity/users/register", registerRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        Assert.Equal("new_admin@example.com", user.Email);
        Assert.Equal(IdentityProfiles.System, user.ProfileName);
    }

    [Fact]
    public async Task Login_ViaHttp_ReturnsAuthTokenDto()
    {
        // 1. Register User
        var registerRequest = new RegisterRequest(
            Email: "shopper_login@example.com",
            UserName: "shopper_login",
            Password: "Password123!",
            ProfileName: IdentityProfiles.User);

        var regResponse = await _client.PostAsJsonAsync("/api/v1/identity/users/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // 2. Login
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(
            Email: "shopper_login@example.com",
            Password: "Password123!"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthTokenDto>(_jsonOptions);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
    }

    [Fact]
    public async Task GetProfiles_ReturnsPublicUserProfile()
    {
        var response = await _client.GetAsync("/api/v1/identity/profiles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profiles = await response.Content.ReadFromJsonAsync<List<ProfileDto>>(_jsonOptions);
        Assert.NotNull(profiles);
        Assert.Contains(profiles, p => p.Name == IdentityProfiles.User);
        // System profile should not be returned for anonymous callers
        Assert.DoesNotContain(profiles, p => p.Name == IdentityProfiles.System);
    }
}
