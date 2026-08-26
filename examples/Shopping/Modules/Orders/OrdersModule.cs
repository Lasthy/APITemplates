using Identity.Contracts.Constants;
using Kernel.Configuration;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Controllers;
using Orders.Persistence;

namespace Orders;

/// <summary>
/// Module registration class for Orders.
/// </summary>
public class OrdersModule : IModule
{
    public static string Name => "Orders";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy("OrdersManage", policy => policy.RequireRole(IdentityRoles.Manager, IdentityRoles.System));
        options.AddPolicy("OrdersStaff", policy => policy.RequireRole(IdentityRoles.Employee, IdentityRoles.Manager, IdentityRoles.System));
        options.AddPolicy("OrdersCustomer", policy => policy.RequireRole(IdentityRoles.Client, IdentityRoles.System));
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register OrdersDbContext
        services.AddDbContext<OrdersDbContext>((sp, options) =>
        {
            var ds = sp.GetService<DynamicSettings>();
            DataSettings? dataSettings = null;
            try { dataSettings = ds?.GetSection<DataSettings>(); } catch { }
            dataSettings ??= sp.GetService<DataSettings>();

            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "orders");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=orders.db", "orders");
            }
            options.AddKernelInterceptors(sp);
        });

        // Add module controllers
        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
    }

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via controllers in UseKernel()
    }
}
