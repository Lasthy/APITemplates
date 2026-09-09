using System.Security.Claims;
using Kernel.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;
using MultiTenancy.Resolution;
using Xunit;

namespace MultiTenancy.Tests;

public class TenantResolutionTests
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly TenantSettings _settings;
    private readonly Tenant _testTenant;

    public TenantResolutionTests()
    {
        var options = new DbContextOptionsBuilder<TenantModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TenantModuleDbContext(options);
        _settings = new TenantSettings(new JsonSerializer());

        _testTenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = "Acme Corp",
            Slug = "acme",
            Status = TenantStatus.Active,
            OwnerUserId = DefaultId.NewUlid(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(_testTenant);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task HeaderTenantResolver_ResolvesByUlid_And_BySlug()
    {
        var resolver = new HeaderTenantResolver(_settings, _dbContext);

        // 1. Resolve by X-Tenant-Id with ULID
        var context1 = new DefaultHttpContext();
        context1.Request.Headers["X-Tenant-Id"] = _testTenant.Id.ToString();
        var result1 = await resolver.ResolveAsync(context1);
        Assert.Equal(_testTenant.Id, result1);

        // 2. Resolve by X-Tenant-Id with slug
        var context2 = new DefaultHttpContext();
        context2.Request.Headers["X-Tenant-Id"] = "acme";
        var result2 = await resolver.ResolveAsync(context2);
        Assert.Equal(_testTenant.Id, result2);

        // 3. Resolve by X-Tenant-Slug
        var context3 = new DefaultHttpContext();
        context3.Request.Headers["X-Tenant-Slug"] = "acme";
        var result3 = await resolver.ResolveAsync(context3);
        Assert.Equal(_testTenant.Id, result3);
    }

    [Fact]
    public async Task ClaimTenantResolver_ResolvesFromTenantIdClaim_And_TenantSlugClaim()
    {
        var resolver = new ClaimTenantResolver(_dbContext);

        // 1. By tenant_id claim
        var claims1 = new[] { new Claim(TenantClaimTypes.TenantId, _testTenant.Id.ToString()) };
        var context1 = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims1, "TestAuth"))
        };
        var result1 = await resolver.ResolveAsync(context1);
        Assert.Equal(_testTenant.Id, result1);

        // 2. By tenant_slug claim
        var claims2 = new[] { new Claim(TenantClaimTypes.TenantSlug, "acme") };
        var context2 = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims2, "TestAuth"))
        };
        var result2 = await resolver.ResolveAsync(context2);
        Assert.Equal(_testTenant.Id, result2);
    }

    [Fact]
    public async Task SubdomainTenantResolver_ResolvesFromHost_And_IgnoresReserved()
    {
        var resolver = new SubdomainTenantResolver(_dbContext);

        // Valid subdomain
        var context1 = new DefaultHttpContext();
        context1.Request.Host = new HostString("acme.api.example.com");
        var result1 = await resolver.ResolveAsync(context1);
        Assert.Equal(_testTenant.Id, result1);

        // Reserved subdomain (e.g. www, api)
        var context2 = new DefaultHttpContext();
        context2.Request.Host = new HostString("api.example.com");
        var result2 = await resolver.ResolveAsync(context2);
        Assert.Null(result2);
    }

    [Fact]
    public async Task RouteSegmentTenantResolver_ResolvesFromRouteValues()
    {
        var resolver = new RouteSegmentTenantResolver(_dbContext);

        var context = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["tenantSlug"] = "acme";
        context.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = routeData });

        var result = await resolver.ResolveAsync(context);
        Assert.Equal(_testTenant.Id, result);
    }

    [Fact]
    public async Task CompositeTenantResolver_ChainsResolversAccordingToPriority()
    {
        var headerResolver = new HeaderTenantResolver(_settings, _dbContext);
        var claimResolver = new ClaimTenantResolver(_dbContext);
        var subdomainResolver = new SubdomainTenantResolver(_dbContext);
        var routeResolver = new RouteSegmentTenantResolver(_dbContext);

        var composite = new CompositeTenantResolver(
            _settings,
            headerResolver,
            claimResolver,
            subdomainResolver,
            routeResolver);

        // Request with header should resolve via header
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = _testTenant.Id.ToString();

        var result = await composite.ResolveAsync(context);
        Assert.Equal(_testTenant.Id, result);
    }

    private class RoutingFeature : IRoutingFeature
    {
        public RouteData RouteData { get; set; } = new();
    }
}
