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
    [InlineData(IdentityRoles.Buyer, IdentityPolicies.Buyer, true)]
    [InlineData(IdentityRoles.Employee, IdentityPolicies.Buyer, true)]
    [InlineData(IdentityRoles.Admin, IdentityPolicies.Buyer, true)]
    [InlineData(IdentityRoles.System, IdentityPolicies.Buyer, true)]
    [InlineData(IdentityRoles.Buyer, IdentityPolicies.Employee, false)]
    [InlineData(IdentityRoles.Employee, IdentityPolicies.Employee, true)]
    [InlineData(IdentityRoles.Admin, IdentityPolicies.Employee, true)]
    [InlineData(IdentityRoles.System, IdentityPolicies.Employee, true)]
    [InlineData(IdentityRoles.Buyer, IdentityPolicies.Admin, false)]
    [InlineData(IdentityRoles.Employee, IdentityPolicies.Admin, false)]
    [InlineData(IdentityRoles.Admin, IdentityPolicies.Admin, true)]
    [InlineData(IdentityRoles.System, IdentityPolicies.Admin, true)]
    public async Task Policies_AuthorizeRolesCorrectly(string userRole, string policyName, bool shouldSucceed)
    {
        var authService = Services.GetRequiredService<IAuthorizationService>();

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Ulid.NewUlid().ToString()),
            new Claim(ClaimTypes.Role, userRole)
        ], "TestAuth"));

        var result = await authService.AuthorizeAsync(user, policyName);

        Assert.Equal(shouldSucceed, result.Succeeded);
    }
}
