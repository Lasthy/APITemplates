using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Authorization;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;
using MultiTenancy.Services;
using Xunit;

namespace MultiTenancy.Tests;

public class AuthorizationPolicyTests
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly FakeScopeContext _scopeContext;
    private readonly Tenant _tenant;

    private readonly DefaultId _ownerId = DefaultId.NewUlid();
    private readonly DefaultId _adminId = DefaultId.NewUlid();
    private readonly DefaultId _memberId = DefaultId.NewUlid();
    private readonly DefaultId _outsiderId = DefaultId.NewUlid();

    public AuthorizationPolicyTests()
    {
        var options = new DbContextOptionsBuilder<TenantModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TenantModuleDbContext(options);
        _scopeContext = new FakeScopeContext().AsAuthenticated();
        _tenantContext = new TenantContext(new EmptyServiceProvider(), _scopeContext);

        _tenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = "Auth Corp",
            Slug = "auth-corp",
            Status = TenantStatus.Active,
            OwnerUserId = _ownerId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(_tenant);
        _dbContext.Members.AddRange(
            new TenantMember { Id = DefaultId.NewUlid(), TenantId = _tenant.Id, UserId = _ownerId, Role = TenantRoles.Owner, JoinedAt = DateTime.UtcNow },
            new TenantMember { Id = DefaultId.NewUlid(), TenantId = _tenant.Id, UserId = _adminId, Role = TenantRoles.Admin, JoinedAt = DateTime.UtcNow },
            new TenantMember { Id = DefaultId.NewUlid(), TenantId = _tenant.Id, UserId = _memberId, Role = TenantRoles.Member, JoinedAt = DateTime.UtcNow }
        );
        _dbContext.SaveChanges();

        _tenantContext.SetTenantId(_tenant.Id);
    }

    [Fact]
    public async Task TenantMemberPolicy_AllowsMembersAndSystem_RejectsOutsider()
    {
        var policy = new TenantMemberPolicy(_dbContext, _tenantContext);

        // Member
        _scopeContext.AsAuthenticated(_memberId);
        var res1 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res1.IsSuccess);

        // Admin
        _scopeContext.AsAuthenticated(_adminId);
        var res2 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res2.IsSuccess);

        // Owner
        _scopeContext.AsAuthenticated(_ownerId);
        var res3 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res3.IsSuccess);

        // System
        _scopeContext.AsSystem(_outsiderId);
        var resSystem = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(resSystem.IsSuccess);

        // Outsider
        _scopeContext.AsAuthenticated(_outsiderId);
        var resOutsider = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(resOutsider.IsFailure);
    }

    [Fact]
    public async Task TenantAdminPolicy_AllowsAdminOwnerAndSystem_RejectsRegularMember()
    {
        var policy = new TenantAdminPolicy(_dbContext, _tenantContext);

        // Owner
        _scopeContext.AsAuthenticated(_ownerId);
        var res1 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res1.IsSuccess);

        // Admin
        _scopeContext.AsAuthenticated(_adminId);
        var res2 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res2.IsSuccess);

        // Regular Member
        _scopeContext.AsAuthenticated(_memberId);
        var res3 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res3.IsFailure);

        // System
        _scopeContext.AsSystem(_outsiderId);
        var resSystem = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(resSystem.IsSuccess);
    }

    [Fact]
    public async Task TenantOwnerPolicy_AllowsOwnerAndSystem_RejectsAdminAndMember()
    {
        var policy = new TenantOwnerPolicy(_dbContext, _tenantContext);

        // Owner
        _scopeContext.AsAuthenticated(_ownerId);
        var res1 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res1.IsSuccess);

        // Admin
        _scopeContext.AsAuthenticated(_adminId);
        var res2 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res2.IsFailure);

        // Regular Member
        _scopeContext.AsAuthenticated(_memberId);
        var res3 = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(res3.IsFailure);

        // System
        _scopeContext.AsSystem(_outsiderId);
        var resSystem = await policy.AuthorizeAsync(_scopeContext, null);
        Assert.True(resSystem.IsSuccess);
    }

    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
