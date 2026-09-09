using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a tenant is suspended.
/// </summary>
public record TenantSuspendedEvent(
    DefaultId TenantId,
    string? Reason,
    DefaultId? SuspendedBy) : Event;
