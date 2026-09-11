using System.Security.Claims;
using Identity.Contracts.Constants;
using Identity.Persistence;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class AuthorizationPolicyTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Theory]
    [InlineData(IdentityRoles.System, IdentityPolicies.System, true)]
    [InlineData("SomeOtherRole", IdentityPolicies.System, false)]
    public async Task Policies_System_AuthorizesRoleCorrectly(string userRole, string policyName, bool shouldSucceed)
    {
        var authService = Services.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Ulid.NewUlid().ToString()),
            new Claim(ClaimTypes.Role, userRole)
        ], "TestAuth"));

        var result = await authService.AuthorizeAsync(user, policyName);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    [Fact]
    public async Task Policies_Authenticated_RequiresAuthenticatedUser()
    {
        var authService = Services.GetRequiredService<IAuthorizationService>();

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var anonResult = await authService.AuthorizeAsync(anonymousUser, IdentityPolicies.Authenticated);
        Assert.False(anonResult.Succeeded);

        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Ulid.NewUlid().ToString())
        ], "TestAuth"));
        var authResult = await authService.AuthorizeAsync(authenticatedUser, IdentityPolicies.Authenticated);
        Assert.True(authResult.Succeeded);
    }
}
