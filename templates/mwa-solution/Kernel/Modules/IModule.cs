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
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Registers module stuff in the DI container.
    /// </summary>
    static abstract void Register(IServiceCollection services, IConfiguration configuration);
}