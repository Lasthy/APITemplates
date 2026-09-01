using Identity.Contracts.Constants;
using Identity.Persistence;
using Identity.Profiles;
using Kernel.Testing.Assertions;
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

        Assert.Contains(profiles, p => p.Name == IdentityProfiles.Buyer);
        Assert.Contains(profiles, p => p.Name == IdentityProfiles.Employee);
        Assert.Contains(profiles, p => p.Name == IdentityProfiles.Admin);
        Assert.Contains(profiles, p => p.Name == IdentityProfiles.System);
    }

    [Fact]
    public void BuyerProfile_IsPublic_AndHasBuyerRole()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var buyer = registry.Get(IdentityProfiles.Buyer);

        Assert.NotNull(buyer);
        Assert.True(buyer.IsPublic);
        Assert.Contains(IdentityRoles.Buyer, buyer.Roles);
    }

    [Theory]
    [InlineData(IdentityProfiles.Employee)]
    [InlineData(IdentityProfiles.Admin)]
    [InlineData(IdentityProfiles.System)]
    public void InternalProfiles_AreNotPublic(string profileName)
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var profile = registry.Get(profileName);

        Assert.NotNull(profile);
        Assert.False(profile.IsPublic);
    }

    [Fact]
    public void AdminProfile_ContainsAdminEmployeeAndBuyerRoles()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var admin = registry.Get(IdentityProfiles.Admin);

        Assert.NotNull(admin);
        Assert.Contains(IdentityRoles.Admin, admin.Roles);
        Assert.Contains(IdentityRoles.Employee, admin.Roles);
        Assert.Contains(IdentityRoles.Buyer, admin.Roles);
    }

    [Fact]
    public void SystemProfile_ContainsSystemAdminEmployeeAndBuyerRoles()
    {
        var registry = Services.GetRequiredService<ProfileRegistry>();
        var system = registry.Get(IdentityProfiles.System);

        Assert.NotNull(system);
        Assert.Contains(IdentityRoles.System, system.Roles);
        Assert.Contains(IdentityRoles.Admin, system.Roles);
        Assert.Contains(IdentityRoles.Employee, system.Roles);
        Assert.Contains(IdentityRoles.Buyer, system.Roles);
    }
}
