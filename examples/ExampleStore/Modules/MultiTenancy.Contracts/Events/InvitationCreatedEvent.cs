using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when an invitation is sent to an email address to join a tenant.
/// </summary>
public record InvitationCreatedEvent(
    DefaultId TenantId,
    string Email,
    string Role,
    DefaultId? InvitedBy) : Event;
