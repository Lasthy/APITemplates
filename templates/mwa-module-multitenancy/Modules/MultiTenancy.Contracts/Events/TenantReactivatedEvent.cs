using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a suspended tenant is reactivated.
/// </summary>
public record TenantReactivatedEvent(
    DefaultId TenantId,
    DefaultId? ReactivatedBy) : Event;
