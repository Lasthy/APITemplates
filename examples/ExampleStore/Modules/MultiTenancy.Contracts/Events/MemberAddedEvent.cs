using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a user is added as a member of a tenant.
/// </summary>
public record MemberAddedEvent(
    DefaultId TenantId,
    DefaultId UserId,
    string Role) : Event;
