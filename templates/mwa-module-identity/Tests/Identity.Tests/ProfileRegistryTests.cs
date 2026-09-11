using Identity.Contracts.Constants;
using Identity.Persistence;
using Identity.Profiles;
using Kernel.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class ProfileRegistryTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Fact]
    public void ProfileRegistry_ContainsExpectedProfiles()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var profiles = registry.GetAll().ToList();

        Assert.Contains(profiles, p => p.Name == IdentityProfiles.User);
        Assert.Contains(profiles, p => p.Name == IdentityProfiles.System);
    }

    [Fact]
    public void UserProfile_IsPublic_AndHasEmptyRolesByDefault()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var user = registry.Get(IdentityProfiles.User);

        Assert.NotNull(user);
        Assert.True(user.IsPublic);
        Assert.Empty(user.Roles);
    }

    [Fact]
    public void SystemProfile_IsNotPublic_AndHasSystemRole()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var system = registry.Get(IdentityProfiles.System);

        Assert.NotNull(system);
        Assert.False(system.IsPublic);
        Assert.Contains(IdentityRoles.System, system.Roles);
    }
}
