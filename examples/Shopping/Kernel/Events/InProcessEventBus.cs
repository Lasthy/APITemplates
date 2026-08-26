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
    /// </summary>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler != null)
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