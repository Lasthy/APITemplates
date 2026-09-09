namespace MultiTenancy.Contracts.Entities;

/// <summary>
/// Marker interface for entities that belong to a specific tenant.
/// Entities implementing this interface automatically participate in EF Core global query filtering
/// and automatic tenant ID stamping upon SaveChanges.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The unique identifier of the tenant owning this entity.
    /// </summary>
    DefaultId TenantId { get; set; }
}
