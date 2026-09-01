using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Modules;


/// <summary>
/// Contract that every module must implement.
/// Responsible for registering services and HTTP endpoints for the module.
/// </summary>
public interface IModule
{
    /// <summary>
    /// The module name, used as the identifier for the OpenAPI document.
    /// Must match the key used in <c>ActiveModules</c> in <c>appsettings.Instance.json</c>.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Registers module services in the DI container.
    /// Called during <c>builder.AddModules()</c> before the host is built.
    /// </summary>
    static abstract void Register(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Registers authorization policies for the module.
    /// </summary>
    static abstract void RegisterPolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options);

    /// <summary>
    /// Supported API versions for this module. Defaults to <c>[ 1.0 ]</c>.
    /// </summary>
    static virtual IReadOnlyList<Asp.Versioning.ApiVersion> SupportedVersions => [new Asp.Versioning.ApiVersion(1, 0)];

    /// <summary>
    /// Maps the module's HTTP endpoints onto the application route builder.
    /// Called during <c>app.UseModules()</c> after the host is built.
    /// </summary>
    static abstract void MapEndpoints(IEndpointRouteBuilder app);
}