using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Requests;
using MultiTenancy.Entities;
using MultiTenancy.Features;
using MultiTenancy.Persistence;
using Xunit;

namespace MultiTenancy.Tests;

public class CrossModuleRequestTests
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly Tenant _tenant;
    private readonly DefaultId _memberId = DefaultId.NewUlid();
    private readonly DefaultId _adminId = DefaultId.NewUlid();

    public CrossModuleRequestTests()
    {
        var options = new DbContextOptionsBuilder<TenantModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TenantModuleDbContext(options);

        _tenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = "Cross Module Corp",
            Slug = "cross-module",
            Status = TenantStatus.Active,
            Plan = "enterprise",
            OwnerUserId = DefaultId.NewUlid(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(_tenant);
        _dbContext.Members.AddRange(
            new TenantMember { Id = DefaultId.NewUlid(), TenantId = _tenant.Id, UserId = _memberId, Role = TenantRoles.Member, JoinedAt = DateTime.UtcNow },
            new TenantMember { Id = DefaultId.NewUlid(), TenantId = _tenant.Id, UserId = _adminId, Role = TenantRoles.Admin, JoinedAt = DateTime.UtcNow }
        );
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetTenantSummaryHandler_ReturnsTenantDto()
    {
        var handler = new GetTenantSummaryHandler(_dbContext);
        var result = await handler.HandleAsync(new GetTenantSummary(_tenant.Id));

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(_tenant.Id, dto.Id);
        Assert.Equal("Cross Module Corp", dto.Name);
        Assert.Equal("cross-module", dto.Slug);
        Assert.Equal("enterprise", dto.Plan);
    }

    [Fact]
    public async Task ValidateTenantAccessHandler_ValidatesAccessAndRoles()
    {
        var handler = new ValidateTenantAccessHandler(_dbContext);

        // General membership check
        var memberAccess = await handler.HandleAsync(new ValidateTenantAccess(_tenant.Id, _memberId));
        Assert.True(memberAccess.IsSuccess && memberAccess.Value);

        var outsiderAccess = await handler.HandleAsync(new ValidateTenantAccess(_tenant.Id, DefaultId.NewUlid()));
        Assert.True(outsiderAccess.IsSuccess && !outsiderAccess.Value);

        // RequiredRole check
        var memberAsAdmin = await handler.HandleAsync(new ValidateTenantAccess(_tenant.Id, _memberId, TenantRoles.Admin));
        Assert.True(memberAsAdmin.IsSuccess && !memberAsAdmin.Value);

        var adminAsAdmin = await handler.HandleAsync(new ValidateTenantAccess(_tenant.Id, _adminId, TenantRoles.Admin));
        Assert.True(adminAsAdmin.IsSuccess && adminAsAdmin.Value);
    }
}
