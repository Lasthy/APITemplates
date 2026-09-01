namespace Kernel.Events;

/// <summary>
/// Handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">Event type.</typeparam>
public interface IEventHandler<TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Handles the event asynchronously.
    /// </summary>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}