using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a tenant member's role is modified.
/// </summary>
public record MemberRoleChangedEvent(
    DefaultId TenantId,
    DefaultId UserId,
    string OldRole,
    string NewRole) : Event;
