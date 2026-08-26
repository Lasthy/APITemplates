using Catalog.Controllers;
using Catalog.Persistence;
using Identity.Contracts.Constants;
using Kernel.Configuration;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog;

/// <summary>
/// Module registration class for Catalog.
/// </summary>
public class CatalogModule : IModule
{
    public static string Name => "Catalog";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy("CatalogManage", policy => policy.RequireRole(IdentityRoles.Manager, IdentityRoles.System));
        options.AddPolicy("CatalogStock", policy => policy.RequireRole(IdentityRoles.Employee, IdentityRoles.Manager, IdentityRoles.System));
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register CatalogDbContext using database settings from Kernel
        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var ds = sp.GetService<DynamicSettings>();
            DataSettings? dataSettings = null;
            try { dataSettings = ds?.GetSection<DataSettings>(); } catch { }
            dataSettings ??= sp.GetService<DataSettings>();

            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "catalog");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=catalog.db", "catalog");
            }
            options.AddKernelInterceptors(sp);
        });

        // Add module controllers
        services.AddControllers().AddApplicationPart(typeof(ProductsController).Assembly);
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via controllers in UseKernel()
    }
}
