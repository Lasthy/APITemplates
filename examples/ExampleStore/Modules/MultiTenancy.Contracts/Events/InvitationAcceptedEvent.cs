using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when an invitation is accepted by a user.
/// </summary>
public record InvitationAcceptedEvent(
    DefaultId TenantId,
    DefaultId UserId,
    DefaultId InvitationId) : Event;
