using Kernel.Events;

namespace MultiTenancy.Contracts.Events;

/// <summary>
/// Published when a new tenant is created.
/// </summary>
public record TenantCreatedEvent(
    DefaultId TenantId,
    string Name,
    string Slug,
    DefaultId OwnerUserId) : Event;
