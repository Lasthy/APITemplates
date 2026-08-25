using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a new user is created.
/// Other modules can subscribe to this event via <see cref="IEventHandler{TEvent}"/>.
/// </summary>
public record UserCreatedEvent(DefaultId UserId, string ProfileName, string Email) : Event;
