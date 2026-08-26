using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Events;

/// <summary>
/// Extension methods for registering the event bus on <see cref="WebApplicationBuilder"/>.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the in-process event bus and event type registry.
    /// Called automatically by <c>AddKernel()</c> — use this directly only if you need
    /// to register the event bus without the full kernel setup.
    /// </summary>
    public static void AddEventBus(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
        builder.Services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
    }
}