using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Kernel.Modules;

/// <summary>
/// Accumulates module assemblies at DI-registration time so that post-build
/// startup steps (event type registration, endpoint mapping, startup hooks) can iterate them.
/// Registered as a singleton by <c>AddKernel()</c> and populated by each <c>AddModule&lt;T&gt;()</c> call.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<ModuleDescriptor> _modules = [];

    /// <summary>All registered module descriptors in registration order.</summary>
    public IReadOnlyList<ModuleDescriptor> Modules => _modules;

    internal void Register(ModuleDescriptor descriptor) => _modules.Add(descriptor);
}

/// <summary>
/// Metadata captured for a single registered module.
/// </summary>
/// <param name="Name">The module name (matches <see cref="IModule.Name"/>).</param>
/// <param name="Assembly">The module's assembly, used for event type scanning.</param>
/// <param name="MapEndpoints">Delegate that maps the module's HTTP endpoints onto a route builder.</param>
/// <param name="OnStartup">Optional delegate executed during startup for module-specific initialization.</param>
public sealed record ModuleDescriptor(
    string Name,
    Assembly Assembly,
    Action<IEndpointRouteBuilder> MapEndpoints,
    Action<IServiceProvider>? OnStartup = null
);
