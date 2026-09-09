using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a tenant is deleted (soft-deleted).
/// </summary>
public record TenantDeletedEvent(
    DefaultId TenantId,
    DefaultId? DeletedBy) : Event;
