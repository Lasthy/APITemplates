using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a user is removed from a tenant's membership.
/// </summary>
public record MemberRemovedEvent(
    DefaultId TenantId,
    DefaultId UserId,
    DefaultId? RemovedBy) : Event;
