using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule;

/// <summary>
/// Module registration class for <c>MyModule</c>.
/// Implements <see cref="IModule"/> to integrate with the Kernel module loading system.
/// </summary>
/// <remarks>
/// This class is discovered automatically by the Kernel's <c>AddModules()</c> extension method.
/// To activate this module, set <c>"MyModule": true</c> in <c>ActiveModules</c>
/// inside <c>appsettings.Instance.json</c>.
/// Any class implementing <see cref="Kernel.Health.IHealthCheck"/> within this assembly is
/// automatically discovered and registered under this module's health checks.
/// </remarks>
public class MyModuleModule : IModule
{
    /// <summary>
    /// The module name — must match the key in <c>ActiveModules</c> in <c>appsettings.Instance.json</c>.
    /// </summary>
    public static string Name => "MyModule";

    /// <summary>
    /// Registers authorization policies for the module.
    /// </summary>
    public static void RegisterPolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        // TODO: Register module-specific authorization policies here.
        // Example:
        //   options.AddPolicy("MyModuleAdmin", policy => policy.RequireRole("MyModuleAdmin"));
    }

    /// <summary>
    /// Registers module-specific services in the DI container.
    /// Called during <c>builder.AddModules()</c>.
    /// </summary>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register module DbContext with dynamic database settings and interceptors
        services.AddDbContext<Persistence.MyModuleDbContext>((sp, options) =>
        {
            var ds = sp.GetService<Kernel.Configuration.DynamicSettings>();
            Kernel.Configuration.DataSettings? dataSettings = null;
            try { dataSettings = ds?.GetSection<Kernel.Configuration.DataSettings>(); } catch { }
            dataSettings ??= sp.GetService<Kernel.Configuration.DataSettings>();

            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "mymodule");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=mymodule.db", "mymodule");
            }
            options.AddKernelInterceptors(sp);
        });

        // TODO: Register module services here.
        // Example:
        //   services.AddScoped<IMyService, MyService>();
    }

    /// <summary>
    /// Maps the module's HTTP endpoints onto the application route builder.
    /// Called during <c>app.UseModules()</c>.
    /// </summary>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // TODO: Map module endpoints here.
        // Example:
        //   var group = app.MapGroup("/api/v1/mymodule").WithTags("MyModule").WithGroupName(Name);
        //   group.MapGet("/", () => Results.Ok("Hello from MyModule!"));
    }
}
