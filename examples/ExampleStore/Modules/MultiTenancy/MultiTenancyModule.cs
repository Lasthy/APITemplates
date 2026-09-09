using Kernel.Configuration;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiTenancy.Authorization;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Controllers;
using MultiTenancy.Middleware;
using MultiTenancy.Persistence;
using MultiTenancy.Resolution;
using MultiTenancy.Services;
using MultiTenancy.Workers;

namespace MultiTenancy;

/// <summary>
/// Module registration class for the MultiTenancy module.
/// Implements <see cref="IModule"/> to integrate with the Kernel module loading system.
/// </summary>
public class MultiTenancyModule : IModule
{
    /// <inheritdoc/>
    public static string Name => "MultiTenancy";

    /// <inheritdoc/>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(TenantPolicies.TenantMember, policy => policy.RequireAuthenticatedUser());
        options.AddPolicy(TenantPolicies.TenantAdmin, policy => policy.RequireAuthenticatedUser());
        options.AddPolicy(TenantPolicies.TenantOwner, policy => policy.RequireAuthenticatedUser());
    }

    /// <inheritdoc/>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // ── Dynamic Settings ───────────────────────────────────────────────────
        var dynamicSettings = services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        TenantSettings tenantSettings;
        if (dynamicSettings != null)
        {
            try { tenantSettings = dynamicSettings.GetSection<TenantSettings>(); }
            catch
            {
                var jsonSerializer = new JsonSerializer();
                tenantSettings = new TenantSettings(jsonSerializer, dynamicSettings.Environment);
                dynamicSettings.AddSection(tenantSettings);
            }
        }
        else
        {
            tenantSettings = new TenantSettings(new JsonSerializer());
        }

        services.AddSingleton(tenantSettings);

        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<TenantModuleDbContext>((sp, options) =>
        {
            var dataSettings = sp.GetService<DataSettings>();
            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "tenancy");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=tenancy.db", "tenancy");
            }
            options.AddKernelInterceptors(sp);
        });

        // ── SaveChanges Interceptor for ITenantScoped data isolation ──────────
        services.AddScoped<ISaveChangesInterceptor, TenantSaveChangesInterceptor>();

        // ── Scoped Context & Services ─────────────────────────────────────────
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantService, TenantService>();

        // ── Resolution Strategies ─────────────────────────────────────────────
        services.AddScoped<HeaderTenantResolver>();
        services.AddScoped<ClaimTenantResolver>();
        services.AddScoped<SubdomainTenantResolver>();
        services.AddScoped<RouteSegmentTenantResolver>();
        services.AddScoped<CompositeTenantResolver>();

        services.AddScoped<ITenantResolver>(sp =>
        {
            var settings = sp.GetRequiredService<TenantSettings>();
            var strategy = settings.ResolutionStrategy.Trim().ToLowerInvariant();

            return strategy switch
            {
                "claim" => sp.GetRequiredService<ClaimTenantResolver>(),
                "subdomain" => sp.GetRequiredService<SubdomainTenantResolver>(),
                "route" => sp.GetRequiredService<RouteSegmentTenantResolver>(),
                "composite" => sp.GetRequiredService<CompositeTenantResolver>(),
                _ => sp.GetRequiredService<HeaderTenantResolver>()
            };
        });

        // ── Authorization Policies ────────────────────────────────────────────
        services.AddScoped<TenantMemberPolicy>();
        services.AddScoped<TenantAdminPolicy>();
        services.AddScoped<TenantOwnerPolicy>();

        // ── Controllers ───────────────────────────────────────────────────────
        services.AddControllers().AddApplicationPart(typeof(TenantsController).Assembly);

        // ── Background Workers ────────────────────────────────────────────────
        services.AddHostedService<InvitationCleanupWorker>();
    }

    /// <inheritdoc/>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        if (app is IApplicationBuilder appBuilder)
        {
            appBuilder.UseMiddleware<TenantResolutionMiddleware>();
        }
    }
}
