using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cysharp.Serialization.Json;
using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Identity.Entities;
using Identity.Profiles;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Testing.Fakes;
using Kernel.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Identity.Controllers.UsersController;

namespace Identity.Tests;

/// <summary>
/// Comprehensive unit and integration tests for the profile creation requirement system.
/// </summary>
public class ProfileCreationRequirementTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProfileCreationRequirementTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new UlidJsonConverter());
    }

    private static ProfileDescriptor CreateDummyDescriptor(
        string name,
        bool isPublic = false,
        IReadOnlyList<IProfileCreationRequirement>? requirements = null)
    {
        return new ProfileDescriptor(
            Name: name,
            Description: $"Test profile {name}",
            IsPublic: isPublic,
            Roles: [],
            Claims: new Dictionary<string, string>(),
            PreferencesType: null,
            OnUserCreated: (_, _) => Task.CompletedTask,
            CreationRequirements: requirements);
    }

    #region Unit Tests - Individual Requirements

    [Fact]
    public async Task PublicRequirement_AlwaysSucceeds_AndReportsPublic()
    {
        var req = ProfileRequirements.Public;
        Assert.True(req.IsPublic);

        var scope = new FakeScopeContext().AsAnonymous();
        var descriptor = CreateDummyDescriptor("TestPublic", isPublic: true);
        var context = new ProfileCreationRequirementContext
        {
            ScopeContext = scope,
            Profile = descriptor
        };

        var result = await req.EvaluateAsync(context);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RequireAuthenticated_FailsForAnonymous_SucceedsForAuthenticated()
    {
        var req = ProfileRequirements.RequireAuthenticated();
        Assert.False(req.IsPublic);

        var descriptor = CreateDummyDescriptor("TestAuth");

        // Anonymous
        var anonScope = new FakeScopeContext().AsAnonymous();
        var anonCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = anonScope,
            Profile = descriptor
        };
        var anonResult = await req.EvaluateAsync(anonCtx);
        Assert.True(anonResult.IsFailure);

        // Authenticated
        var authedScope = new FakeScopeContext().AsAuthenticated();
        var authedCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = authedScope,
            Profile = descriptor
        };
        var authedResult = await req.EvaluateAsync(authedCtx);
        Assert.True(authedResult.IsSuccess);
    }

    [Fact]
    public async Task RequireSystem_FailsForNormalUser_SucceedsForSystem()
    {
        var req = ProfileRequirements.RequireSystem();
        var descriptor = CreateDummyDescriptor("TestSystem");

        // Normal authenticated user
        var normalScope = new FakeScopeContext().AsAuthenticated(null, "User");
        var normalCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = normalScope,
            Profile = descriptor
        };
        var normalResult = await req.EvaluateAsync(normalCtx);
        Assert.True(normalResult.IsFailure);

        // System super-user
        var systemScope = new FakeScopeContext().AsSystem();
        var systemCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = systemScope,
            Profile = descriptor
        };
        var systemResult = await req.EvaluateAsync(systemCtx);
        Assert.True(systemResult.IsSuccess);
    }

    [Fact]
    public async Task RequireRole_EvaluatesRoleMembership_AndAllowsSystem()
    {
        var req = ProfileRequirements.RequireAnyRole("Admin", "Manager");
        var descriptor = CreateDummyDescriptor("TestStaff");

        // Missing role
        var buyerScope = new FakeScopeContext().AsAuthenticated(null, "Buyer");
        var buyerCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = buyerScope,
            Profile = descriptor
        };
        var buyerResult = await req.EvaluateAsync(buyerCtx);
        Assert.True(buyerResult.IsFailure);

        // Matching first role
        var adminScope = new FakeScopeContext().AsAuthenticated(null, "Admin");
        var adminCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = adminScope,
            Profile = descriptor
        };
        var adminResult = await req.EvaluateAsync(adminCtx);
        Assert.True(adminResult.IsSuccess);

        // Matching second role
        var mgrScope = new FakeScopeContext().AsAuthenticated(null, "Manager");
        var mgrCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = mgrScope,
            Profile = descriptor
        };
        var mgrResult = await req.EvaluateAsync(mgrCtx);
        Assert.True(mgrResult.IsSuccess);

        // System bypass
        var sysScope = new FakeScopeContext().AsSystem();
        var sysCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = sysScope,
            Profile = descriptor
        };
        var sysResult = await req.EvaluateAsync(sysCtx);
        Assert.True(sysResult.IsSuccess);
    }

    [Fact]
    public async Task RequireAllRoles_RequiresAllRolesToBePresent()
    {
        var req = ProfileRequirements.RequireAllRoles("Manager", "HR");
        var descriptor = CreateDummyDescriptor("TestDirector");

        // Only Manager
        var partialScope = new FakeScopeContext().AsAuthenticated(null, "Manager");
        var partialCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = partialScope,
            Profile = descriptor
        };
        var partialResult = await req.EvaluateAsync(partialCtx);
        Assert.True(partialResult.IsFailure);

        // Both Manager and HR
        var fullScope = new FakeScopeContext().AsAuthenticated(null, "Manager", "HR");
        var fullCtx = new ProfileCreationRequirementContext
        {
            ScopeContext = fullScope,
            Profile = descriptor
        };
        var fullResult = await req.EvaluateAsync(fullCtx);
        Assert.True(fullResult.IsSuccess);
    }

    [Fact]
    public async Task RequireClaim_ValidatesClaimExistenceAndValue()
    {
        var reqWithTypeOnly = ProfileRequirements.RequireClaim("department");
        var reqWithValue = ProfileRequirements.RequireClaim("department", "engineering");
        var descriptor = CreateDummyDescriptor("TestClaim");

        // Missing claim
        var noClaimScope = new FakeScopeContext().AsAuthenticated();
        var ctx1 = new ProfileCreationRequirementContext { ScopeContext = noClaimScope, Profile = descriptor };
        Assert.True((await reqWithTypeOnly.EvaluateAsync(ctx1)).IsFailure);
        Assert.True((await reqWithValue.EvaluateAsync(ctx1)).IsFailure);

        // Has claim with different value
        var salesScope = new FakeScopeContext().AsAuthenticated().WithClaim("department", "sales");
        var ctx2 = new ProfileCreationRequirementContext { ScopeContext = salesScope, Profile = descriptor };
        Assert.True((await reqWithTypeOnly.EvaluateAsync(ctx2)).IsSuccess);
        Assert.True((await reqWithValue.EvaluateAsync(ctx2)).IsFailure);

        // Has claim with matching value
        var engScope = new FakeScopeContext().AsAuthenticated().WithClaim("department", "engineering");
        var ctx3 = new ProfileCreationRequirementContext { ScopeContext = engScope, Profile = descriptor };
        Assert.True((await reqWithValue.EvaluateAsync(ctx3)).IsSuccess);
    }

    [Fact]
    public async Task RequireCallerProfile_ValidatesCallerProfileClaim()
    {
        var req = ProfileRequirements.RequireCallerProfile("TenantAdmin");
        var descriptor = CreateDummyDescriptor("TenantMember");

        var userScope = new FakeScopeContext().AsAuthenticated().WithClaim(IdentityClaimTypes.Profile, "User");
        var ctx1 = new ProfileCreationRequirementContext { ScopeContext = userScope, Profile = descriptor };
        Assert.True((await req.EvaluateAsync(ctx1)).IsFailure);

        var tenantAdminScope = new FakeScopeContext().AsAuthenticated().WithClaim(IdentityClaimTypes.Profile, "TenantAdmin");
        var ctx2 = new ProfileCreationRequirementContext { ScopeContext = tenantAdminScope, Profile = descriptor };
        Assert.True((await req.EvaluateAsync(ctx2)).IsSuccess);
    }

    [Fact]
    public async Task Combinators_AnyAndAll_EvaluateProperly()
    {
        var descriptor = CreateDummyDescriptor("Composite");

        // Any (OR)
        var anyReq = ProfileRequirements.Any(
            ProfileRequirements.RequireRole("Admin"),
            ProfileRequirements.RequireClaim("vip", "true"));

        var adminOnly = new FakeScopeContext().AsAuthenticated(null, "Admin");
        Assert.True((await anyReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = adminOnly, Profile = descriptor })).IsSuccess);

        var vipOnly = new FakeScopeContext().AsAuthenticated().WithClaim("vip", "true");
        Assert.True((await anyReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = vipOnly, Profile = descriptor })).IsSuccess);

        var neither = new FakeScopeContext().AsAuthenticated();
        Assert.True((await anyReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = neither, Profile = descriptor })).IsFailure);

        // All (AND)
        var allReq = ProfileRequirements.All(
            ProfileRequirements.RequireRole("Admin"),
            ProfileRequirements.RequireClaim("vip", "true"));

        Assert.True((await allReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = adminOnly, Profile = descriptor })).IsFailure);
        Assert.True((await allReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = vipOnly, Profile = descriptor })).IsFailure);

        var both = new FakeScopeContext().AsAuthenticated(null, "Admin").WithClaim("vip", "true");
        Assert.True((await allReq.EvaluateAsync(new ProfileCreationRequirementContext { ScopeContext = both, Profile = descriptor })).IsSuccess);
    }

    [Fact]
    public async Task CustomRequirement_EvaluatesCustomPredicate()
    {
        var req = ProfileRequirements.Custom(ctx => ctx.Email?.EndsWith("@company.com") == true, "Company email required");
        var descriptor = CreateDummyDescriptor("InternalProfile");

        var invalidEmail = new ProfileCreationRequirementContext
        {
            ScopeContext = new FakeScopeContext().AsAuthenticated(),
            Profile = descriptor,
            Email = "john@gmail.com"
        };
        var res1 = await req.EvaluateAsync(invalidEmail);
        Assert.True(res1.IsFailure);
        Assert.Equal("Company email required", res1.Error?.Message);

        var validEmail = new ProfileCreationRequirementContext
        {
            ScopeContext = new FakeScopeContext().AsAuthenticated(),
            Profile = descriptor,
            Email = "alice@company.com"
        };
        var res2 = await req.EvaluateAsync(validEmail);
        Assert.True(res2.IsSuccess);
    }

    #endregion

    #region Integration Tests - Registration Endpoint with Profile Requirements

    [Fact]
    public async Task Register_BuyerProfile_AnonymousSucceeds()
    {
        var request = new RegisterRequest(
            Email: $"buyer_test_{Ulid.NewUlid()}@example.com",
            UserName: $"buyer_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Buyer);

        var response = await _client.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        Assert.Equal(IdentityProfiles.Buyer, user.ProfileName);
    }

    [Fact]
    public async Task Register_EmployeeProfile_AnonymousReturnsForbidden()
    {
        // Employee profile requires IdentityRoles.Admin; anonymous must be rejected
        var request = new RegisterRequest(
            Email: $"emp_anon_{Ulid.NewUlid()}@example.com",
            UserName: $"emp_anon_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Employee);

        var response = await _client.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmployeeProfile_BuyerReturnsForbidden()
    {
        // Buyer does not have Admin role; must be forbidden
        var buyerClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.Buyer);

        var request = new RegisterRequest(
            Email: $"emp_by_buyer_{Ulid.NewUlid()}@example.com",
            UserName: $"emp_by_buyer_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Employee);

        var response = await buyerClient.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmployeeProfile_AdminSucceeds()
    {
        // Admin role satisfies EmployeeProfile's RequireRole(IdentityRoles.Admin)
        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.Admin);

        var email = $"emp_by_admin_{Ulid.NewUlid()}@example.com";
        var request = new RegisterRequest(
            Email: email,
            UserName: $"emp_by_admin_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Employee);

        var response = await adminClient.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        Assert.Equal(IdentityProfiles.Employee, user.ProfileName);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task Register_AdminProfile_AdminReturnsForbidden_BecauseRequiresSystem()
    {
        // AdminProfile requires System super-user role, so regular Admin cannot create Admin
        var adminClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.Admin);

        var request = new RegisterRequest(
            Email: $"admin_by_admin_{Ulid.NewUlid()}@example.com",
            UserName: $"admin_by_admin_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Admin);

        var response = await adminClient.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_AdminProfile_SystemSucceeds()
    {
        // System super-user can create Admin profile
        var systemClient = _factory.CreateAuthenticatedClient(Ulid.NewUlid(), IdentityRoles.System);

        var email = $"admin_by_sys_{Ulid.NewUlid()}@example.com";
        var request = new RegisterRequest(
            Email: email,
            UserName: $"admin_by_sys_{Ulid.NewUlid()}",
            Password: "SecurePassword123!",
            ProfileName: IdentityProfiles.Admin);

        var response = await systemClient.PostAsJsonAsync("/api/v1/identity/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
        Assert.NotNull(user);
        Assert.Equal(IdentityProfiles.Admin, user.ProfileName);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task GetProfile_ReturnsRequirementsInDto()
    {
        var response = await _client.GetAsync($"/api/v1/identity/profiles/{IdentityProfiles.Employee}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profileDetail = await response.Content.ReadFromJsonAsync<ProfileDetailDto>(_jsonOptions);
        Assert.NotNull(profileDetail);
        Assert.NotNull(profileDetail.Requirements);
        Assert.Contains(profileDetail.Requirements, r => r.Contains("Admin", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
