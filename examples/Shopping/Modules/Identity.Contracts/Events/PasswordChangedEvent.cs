using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a user successfully changes their password.
/// Other modules can subscribe to this event to send security alerts, audit logs, etc.
/// </summary>
public record PasswordChangedEvent(DefaultId UserId) : Event;
