using Kernel.Events;

namespace MyModule.Contracts.Events;

/// <summary>
/// Domain event published when a MyModule entity is created.
/// Other modules can subscribe to this event via <see cref="Kernel.Events.IEventHandler{TEvent}"/>.
/// </summary>
public record MyModuleCreatedEvent(DefaultId ItemId, string Name) : Event;
