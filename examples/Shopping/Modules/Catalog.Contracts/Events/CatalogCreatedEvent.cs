using Kernel.Events;

namespace Catalog.Contracts.Events;

/// <summary>
/// Domain event published when a Catalog entity is created.
/// Other modules can subscribe to this event via <see cref="Kernel.Events.IEventHandler{TEvent}"/>.
/// </summary>
public record CatalogCreatedEvent(DefaultId ItemId, string Name) : Event;
