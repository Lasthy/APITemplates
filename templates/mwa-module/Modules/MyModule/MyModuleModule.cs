using Kernel.Modules;
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
/// </remarks>
public class MyModuleModule : IModule
{
    /// <summary>
    /// The module name — must match the key in <c>ActiveModules</c> in <c>appsettings.Instance.json</c>.
    /// </summary>
    public static string Name => "MyModule";

    /// <summary>
    /// Registers module-specific services in the DI container.
    /// Called during <c>builder.AddModules()</c>.
    /// </summary>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
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
        //   var group = app.MapGroup("/mymodule").WithTags("MyModule").WithGroupName(Name);
        //   group.MapGet("/", () => Results.Ok("Hello from MyModule!"));
    }
}
