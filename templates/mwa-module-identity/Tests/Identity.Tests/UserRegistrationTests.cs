using Identity.Contracts.Constants;
using Identity.Contracts.Events;
using Identity.Entities;
using Identity.Features.Users;
using Identity.Persistence;
using Kernel.Testing.Assertions;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests;

public class UserRegistrationTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Fact]
    public async Task CreateUser_UserProfile_SucceedsAndAssignsUserProfile()
    {
        var command = new CreateUser(
            Email: "user@example.com",
            UserName: "user_john",
            Password: "Password123!",
            ProfileName: IdentityProfiles.User);

        var result = await Dispatcher.DispatchAsync(command);

        var userDto = result.ShouldBeSuccess();
        Assert.Equal("user@example.com", userDto.Email);
        Assert.Equal("user_john", userDto.UserName);
        Assert.Equal(IdentityProfiles.User, userDto.ProfileName);

        // Check user in UserManager
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("user@example.com");
        Assert.NotNull(user);

        // Check event published
        EventBus.ShouldHavePublished<UserCreatedEvent>(e => e.Email == "user@example.com" && e.ProfileName == IdentityProfiles.User);
    }

    [Fact]
    public async Task CreateUser_SystemProfile_WithSystemScope_SucceedsAndAssignsSystemRole()
    {
        ScopeContext.AsAuthenticated(null, IdentityRoles.System);

        var command = new CreateUser(
            Email: "admin@example.com",
            UserName: "admin_boss",
            Password: "Password123!",
            ProfileName: IdentityProfiles.System);

        var result = await Dispatcher.DispatchAsync(command);

        var userDto = result.ShouldBeSuccess();
        Assert.Equal(IdentityProfiles.System, userDto.ProfileName);

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(user);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(IdentityRoles.System, roles);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsFailure()
    {
        var command1 = new CreateUser(
            Email: "duplicate@example.com",
            UserName: "user_one",
            Password: "Password123!",
            ProfileName: IdentityProfiles.User);

        var command2 = new CreateUser(
            Email: "duplicate@example.com",
            UserName: "user_two",
            Password: "Password123!",
            ProfileName: IdentityProfiles.User);

        var result1 = await Dispatcher.DispatchAsync(command1);
        result1.ShouldBeSuccess();

        var result2 = await Dispatcher.DispatchAsync(command2);
        result2.ShouldBeFailure();
    }

    [Fact]
    public async Task CreateUser_WithAdditionalData_ForwardsDataToEventAndHook()
    {
        var additionalData = new Dictionary<string, object>
        {
            ["Organization"] = "Acme Global",
            ["TenantId"] = "01JTESTTENANT1234567890",
            ["Tier"] = 2
        };

        var command = new CreateUser(
            Email: "companion_test@example.com",
            UserName: "companion_user",
            Password: "Password123!",
            ProfileName: IdentityProfiles.User,
            AdditionalData: additionalData);

        var result = await Dispatcher.DispatchAsync(command);
        var userDto = result.ShouldBeSuccess();

        Assert.Equal("companion_test@example.com", userDto.Email);

        // Check that event received AdditionalData
        EventBus.ShouldHavePublished<UserCreatedEvent>(e =>
            e.Email == "companion_test@example.com" &&
            e.AdditionalData != null &&
            e.AdditionalData.ContainsKey("Organization") &&
            (string)e.AdditionalData["Organization"] == "Acme Global");
    }
}
