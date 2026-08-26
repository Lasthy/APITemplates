namespace Kernel.Events;

/// <summary>
/// Event bus abstraction.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent;
}