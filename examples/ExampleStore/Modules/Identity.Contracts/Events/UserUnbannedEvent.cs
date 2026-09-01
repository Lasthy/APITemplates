using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a user ban is lifted.
/// </summary>
public record UserUnbannedEvent(DefaultId UserId, DefaultId UnbannedBy) : Event;
