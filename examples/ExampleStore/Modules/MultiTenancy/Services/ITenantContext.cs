using MultiTenancy.Entities;

namespace MultiTenancy.Services;

/// <summary>
/// Scoped accessor for the current request's resolved tenant.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The unique identifier of the resolved tenant for the current request scope,
    /// or null if no tenant was resolved.
    /// </summary>
    DefaultId? TenantId { get; }

    /// <summary>
    /// Whether a tenant was successfully resolved for the current request scope.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Whether multi-tenant global query filters are currently bypassed for this scope/operation.
    /// </summary>
    bool IsFilterBypassed { get; }

    /// <summary>
    /// Returns the resolved tenant entity for the current request scope, lazy-loaded on first call.
    /// Returns null if no tenant is resolved.
    /// </summary>
    Task<Tenant?> GetTenantAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the resolved tenant ID for the current scope.
    /// </summary>
    void SetTenantId(DefaultId? tenantId);
}
