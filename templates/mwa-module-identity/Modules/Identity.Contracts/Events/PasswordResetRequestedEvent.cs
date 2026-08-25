using Kernel.Events;

namespace Identity.Contracts.Events;

/// <summary>
/// Domain event published when a password reset is requested for a user.
/// Other modules (e.g. email/notification services) can subscribe to this event
/// to send the reset token/link to the user's email.
/// </summary>
public record PasswordResetRequestedEvent(
    DefaultId UserId,
    string Email,
    string ResetToken) : Event;
