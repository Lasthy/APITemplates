using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a user successfully resets their password using a reset token.
/// </summary>
public record PasswordResetCompletedEvent(DefaultId UserId, string Email) : Event;
