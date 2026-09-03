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
    public async Task CreateUser_BuyerProfile_SucceedsAndAssignsBuyerRole()
    {
        var command = new CreateUser(
            Email: "buyer@example.com",
            UserName: "buyer_john",
            Password: "Password123!",
            ProfileName: IdentityProfiles.Buyer);

        var result = await Dispatcher.DispatchAsync(command);

        var userDto = result.ShouldBeSuccess();
        Assert.Equal("buyer@example.com", userDto.Email);
        Assert.Equal("buyer_john", userDto.UserName);
        Assert.Equal(IdentityProfiles.Buyer, userDto.ProfileName);

        // Check role in UserManager
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("buyer@example.com");
        Assert.NotNull(user);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(IdentityRoles.Buyer, roles);

        // Check event published
        EventBus.ShouldHavePublished<UserCreatedEvent>(e => e.Email == "buyer@example.com" && e.ProfileName == IdentityProfiles.Buyer);
    }

    [Fact]
    public async Task CreateUser_EmployeeProfile_SucceedsAndAssignsEmployeeRole()
    {
        var command = new CreateUser(
            Email: "employee@examplestore.com",
            UserName: "emp_alice",
            Password: "Password123!",
            ProfileName: IdentityProfiles.Employee);

        var result = await Dispatcher.DispatchAsync(command);

        var userDto = result.ShouldBeSuccess();
        Assert.Equal(IdentityProfiles.Employee, userDto.ProfileName);

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("employee@examplestore.com");
        Assert.NotNull(user);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(IdentityRoles.Employee, roles);
    }

    [Fact]
    public async Task CreateUser_AdminProfile_SucceedsAndAssignsAdminRoles()
    {
        var command = new CreateUser(
            Email: "admin@examplestore.com",
            UserName: "admin_boss",
            Password: "Password123!",
            ProfileName: IdentityProfiles.Admin);

        var result = await Dispatcher.DispatchAsync(command);

        var userDto = result.ShouldBeSuccess();
        Assert.Equal(IdentityProfiles.Admin, userDto.ProfileName);

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("admin@examplestore.com");
        Assert.NotNull(user);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(IdentityRoles.Admin, roles);
        Assert.Contains(IdentityRoles.Employee, roles);
        Assert.Contains(IdentityRoles.Buyer, roles);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsFailure()
    {
        var command1 = new CreateUser(
            Email: "duplicate@example.com",
            UserName: "user_one",
            Password: "Password123!",
            ProfileName: IdentityProfiles.Buyer);

        var command2 = new CreateUser(
            Email: "duplicate@example.com",
            UserName: "user_two",
            Password: "Password123!",
            ProfileName: IdentityProfiles.Buyer);

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
            ProfileName: IdentityProfiles.Buyer,
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
