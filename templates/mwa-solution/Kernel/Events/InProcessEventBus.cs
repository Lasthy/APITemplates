using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Events;

/// <summary>
/// Default in-process implementation of <see cref="IEventBus"/>.
/// Resolves and executes all registered <see cref="IEventHandler{T}"/> instances for each event.
/// </summary>
public sealed class InProcessEventBus(IServiceProvider serviceProvider) : IEventBus
{
    /// <summary>
    /// Publishes an event instance to all subscribed handlers.
    /// Supports polymorphic dispatching to handlers registered for the event's concrete type,
    /// base classes, and implemented event interfaces.
    /// </summary>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent
    {
        var executedHandlers = new HashSet<object>();
        var currentType = @event.GetType();
        var typesToQuery = new List<Type>();

        for (var type = currentType; type != null && typeof(IEvent).IsAssignableFrom(type); type = type.BaseType)
        {
            typesToQuery.Add(type);
        }
        foreach (var iface in currentType.GetInterfaces().Where(i => typeof(IEvent).IsAssignableFrom(i)))
        {
            typesToQuery.Add(iface);
        }

        foreach (var targetType in typesToQuery.Distinct())
        {
            var handlerType = typeof(IEventHandler<>).MakeGenericType(targetType);
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler != null && executedHandlers.Add(handler))
                {
                    var method = handlerType.GetMethod(nameof(IEventHandler<T>.HandleAsync));
                    if (method != null)
                    {
                        var task = (Task)method.Invoke(handler, [@event, cancellationToken])!;
                        await task;
                    }
                }
            }
        }
    }
}