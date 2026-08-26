using Kernel.Events;

namespace Orders.Contracts.Events;

/// <summary>
/// Domain event published when a Orders entity is created.
/// Other modules can subscribe to this event via <see cref="Kernel.Events.IEventHandler{TEvent}"/>.
/// </summary>
public record OrdersCreatedEvent(DefaultId ItemId, string Name) : Event;
