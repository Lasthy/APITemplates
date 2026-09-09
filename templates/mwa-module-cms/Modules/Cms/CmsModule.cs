using Cms.Configuration;
using Cms.ContentTypes;
using Cms.Contracts.Constants;
using Cms.Controllers;
using Cms.Persistence;
using Cms.Services;
using Cms.Workers;
using Kernel.Configuration;
using Kernel.Constants;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms;

/// <summary>
/// Pluggable module registration for the CMS module.
/// Implements <see cref="IModule"/> to integrate with the Kernel module pipeline.
/// </summary>
public class CmsModule : IModule
{
    /// <inheritdoc/>
    public static string Name => "Cms";

    /// <inheritdoc/>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(CmsPolicies.ManageContent, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(Roles.System) ||
                ctx.User.IsInRole("Admin") ||
                ctx.User.IsInRole("Editor") ||
                ctx.User.IsInRole("Publisher") ||
                ctx.User.HasClaim("permission", "cms:manage") ||
                ctx.User.Identity?.IsAuthenticated == true));

        options.AddPolicy(CmsPolicies.PublishContent, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(Roles.System) ||
                ctx.User.IsInRole("Admin") ||
                ctx.User.IsInRole("Publisher") ||
                ctx.User.HasClaim("permission", "cms:publish") ||
                ctx.User.Identity?.IsAuthenticated == true));
    }

    /// <inheritdoc/>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // ── Dynamic Settings ───────────────────────────────────────────────────
        var dynamicSettings = services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        CmsSettings cmsSettings;
        if (dynamicSettings != null)
        {
            try
            {
                cmsSettings = dynamicSettings.GetSection<CmsSettings>();
            }
            catch
            {
                var jsonSerializer = new JsonSerializer();
                cmsSettings = new CmsSettings(jsonSerializer, dynamicSettings.Environment);
                dynamicSettings.AddSection(cmsSettings);
            }
        }
        else
        {
            cmsSettings = new CmsSettings(new JsonSerializer());
        }

        services.AddSingleton(cmsSettings);

        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<CmsModuleDbContext>((sp, options) =>
        {
            var dataSettings = sp.GetService<DataSettings>();
            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "cms");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=cms.db", "cms");
            }
            options.AddKernelInterceptors(sp);
        });

        // ── Content Type System ───────────────────────────────────────────────
        var registry = new ContentTypeRegistry();
        registry.RegisterFromAssembly(typeof(CmsModule).Assembly);
        services.AddSingleton<IContentTypeRegistry>(registry);

        // ── Core Services ─────────────────────────────────────────────────────
        services.AddScoped<ISlugService, SlugService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IContentMediaService, ContentMediaService>();

        // ── Controllers ───────────────────────────────────────────────────────
        services.AddControllers().AddApplicationPart(typeof(ContentController).Assembly);

        // ── Background Workers ────────────────────────────────────────────────
        services.AddHostedService<MediaCleanupWorker>();
        services.AddHostedService<ScheduledPublishingWorker>();
    }

    /// <inheritdoc/>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via API Controllers registered through MVC pipeline
    }
}
