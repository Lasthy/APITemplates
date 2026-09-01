using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Events.Extensions;

/// <summary>
/// Extension methods for configuring and registering event bus infrastructure.
/// </summary>
public static class EventExtensions
{
    /// <summary>
    /// Registers the in-process event bus and event type registry in the DI container.
    /// </summary>
    public static IServiceCollection AddEvents(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InProcessEventBus>();
        services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
        return services;
    }
}
