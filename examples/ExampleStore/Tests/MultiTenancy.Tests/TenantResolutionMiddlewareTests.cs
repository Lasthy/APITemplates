using Kernel.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Entities;
using MultiTenancy.Middleware;
using MultiTenancy.Persistence;
using MultiTenancy.Resolution;
using MultiTenancy.Services;
using Xunit;

namespace MultiTenancy.Tests;

public class TenantResolutionMiddlewareTests
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly TenantSettings _settings;
    private readonly TenantContext _tenantContext;
    private readonly Tenant _activeTenant;
    private readonly Tenant _suspendedTenant;

    public TenantResolutionMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<TenantModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TenantModuleDbContext(options);
        _settings = new TenantSettings(new JsonSerializer());
        _tenantContext = new TenantContext(new EmptyServiceProvider());

        _activeTenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = "Active Tenant",
            Slug = "active",
            Status = TenantStatus.Active,
            OwnerUserId = DefaultId.NewUlid(),
            CreatedAt = DateTime.UtcNow
        };

        _suspendedTenant = new Tenant
        {
            Id = DefaultId.NewUlid(),
            Name = "Suspended Tenant",
            Slug = "suspended",
            Status = TenantStatus.Suspended,
            SuspensionReason = "Payment overdue",
            OwnerUserId = DefaultId.NewUlid(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.AddRange(_activeTenant, _suspendedTenant);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenant_SetsTenantContextAndCallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var resolver = new FakeTenantResolver(_activeTenant.Id);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, resolver, _tenantContext, _settings, _dbContext);

        Assert.True(nextCalled);
        Assert.Equal(_activeTenant.Id, _tenantContext.TenantId);
        Assert.True(_tenantContext.HasTenant);
    }

    [Fact]
    public async Task InvokeAsync_SuspendedTenant_Returns403Forbidden()
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var resolver = new FakeTenantResolver(_suspendedTenant.Id);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, resolver, _tenantContext, _settings, _dbContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTenant_Returns404NotFound()
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var resolver = new FakeTenantResolver(DefaultId.NewUlid()); // Non-existent
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, resolver, _tenantContext, _settings, _dbContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Unresolved_WhenRequireTenantTrue_Returns400()
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        _settings.SetValue(nameof(TenantSettings.RequireTenant), true);

        var middleware = new TenantResolutionMiddleware(next);
        var resolver = new FakeTenantResolver(null);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, resolver, _tenantContext, _settings, _dbContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Unresolved_WhenRequireTenantTrue_WithTenantOptional_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        _settings.SetValue(nameof(TenantSettings.RequireTenant), true);

        var middleware = new TenantResolutionMiddleware(next);
        var resolver = new FakeTenantResolver(null);
        var context = new DefaultHttpContext();

        // Attach [TenantOptional] metadata to endpoint
        var endpointMetadata = new EndpointMetadataCollection(new TenantOptionalAttribute());
        var endpoint = new Endpoint(ctx => Task.CompletedTask, endpointMetadata, "TestEndpoint");
        context.SetEndpoint(endpoint);

        await middleware.InvokeAsync(context, resolver, _tenantContext, _settings, _dbContext);

        Assert.True(nextCalled);
    }

    private class FakeTenantResolver : ITenantResolver
    {
        private readonly DefaultId? _result;

        public FakeTenantResolver(DefaultId? result)
        {
            _result = result;
        }

        public Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
        {
            return Task.FromResult(_result);
        }
    }

    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
