using Identity.Contracts.Constants;
using Identity.Features.Auth;
using Identity.Features.Users;
using Identity.Persistence;
using Kernel.Testing.Assertions;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace Identity.Tests;

public class AuthenticationTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // 1. Create a buyer user
        var createResult = await Dispatcher.DispatchAsync(new CreateUser(
            Email: "buyer.login@example.com",
            UserName: "buyer_login",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.User));
        var user = createResult.ShouldBeSuccess();

        // 2. Login
        var loginResult = await Dispatcher.DispatchAsync(new Login(
            Email: "buyer.login@example.com",
            Password: "SecurePassword123!"));

        var token = loginResult.ShouldBeSuccess();
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFailure()
    {
        // 1. Create user
        var createResult = await Dispatcher.DispatchAsync(new CreateUser(
            Email: "buyer.wrongpass@example.com",
            UserName: "buyer_wrongpass",
            Password: "CorrectPassword123!",
            ProfileName: IdentityProfiles.User));
        createResult.ShouldBeSuccess();

        // 2. Login with wrong password
        var loginResult = await Dispatcher.DispatchAsync(new Login(
            Email: "buyer.wrongpass@example.com",
            Password: "WrongPassword123!"));

        loginResult.ShouldBeFailure();
    }

    [Fact]
    public async Task RefreshToken_RotatesRefreshTokenAndIssuesNewTokens()
    {
        // 1. Create user & login
        await Dispatcher.DispatchAsync(new CreateUser(
            Email: "buyer.refresh@example.com",
            UserName: "buyer_refresh",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.User));

        var loginResult = await Dispatcher.DispatchAsync(new Login(
            Email: "buyer.refresh@example.com",
            Password: "SecurePassword123!"));
        var token = loginResult.ShouldBeSuccess();

        var originalRefreshToken = token.RefreshToken;

        // 2. Refresh token
        var refreshResult = await Dispatcher.DispatchAsync(new RefreshToken(originalRefreshToken));
        var refreshedToken = refreshResult.ShouldBeSuccess();

        Assert.NotEqual(originalRefreshToken, refreshedToken.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshedToken.AccessToken));

        // 3. Re-using old token fails (already revoked)
        var replayResult = await Dispatcher.DispatchAsync(new RefreshToken(originalRefreshToken));
        replayResult.ShouldBeFailure();
    }
}
