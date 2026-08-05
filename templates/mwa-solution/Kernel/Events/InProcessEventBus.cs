using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Events;

/// <summary>
/// Default in-process implementation of <see cref="IEventBus"/>.
/// Resolves and executes all registered <see cref="IEventHandler{T}"/> instances for each event
/// </summary>
public sealed class InProcessEventBus
(
    IServiceProvider serviceProvider) : IEventBus
{
    /// <summary>
    /// Publishes an event instance to all subscribed handlers.
    /// If the event is an <see cref="IntegrationEvent"/>, it is forwarded to an <see cref="IIntegrationEventPublisher"/>.
    /// </summary>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent
    {
        // Resolve all handlers registered for this event type.
        var handlers = serviceProvider.GetServices<IEventHandler<T>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(@event, cancellationToken);
        }
    }
}