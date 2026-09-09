using Kernel.Context;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Entities;
using MultiTenancy.Persistence;

namespace MultiTenancy.Services;

/// <summary>
/// Default scoped implementation of <see cref="ITenantContext"/>.
/// Provides ambient tenant state for EF Core query filters via <see cref="AsyncLocal{T}"/>.
/// </summary>
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<DefaultId?> AmbientTenantId = new();
    private static readonly AsyncLocal<bool> AmbientBypassFilter = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly IScopeContext? _scopeContext;
    private DefaultId? _scopedTenantId;
    private Tenant? _cachedTenant;
    private bool _tenantLoaded;

    /// <summary>
    /// Gets the current ambient tenant ID for the current execution context.
    /// Used by EF Core query filters.
    /// </summary>
    public static DefaultId? CurrentTenantId => AmbientTenantId.Value;

    /// <summary>
    /// Evaluates whether the EF Core multi-tenant query filter should be bypassed
    /// (e.g. for System users or explicit bypass scopes).
    /// </summary>
    public static bool ShouldBypassFilter()
    {
        return AmbientBypassFilter.Value;
    }

    /// <summary>
    /// Sets ambient bypass mode for the current execution context.
    /// </summary>
    public static void SetBypassFilter(bool bypass)
    {
        AmbientBypassFilter.Value = bypass;
    }

    public TenantContext(IServiceProvider serviceProvider, IScopeContext? scopeContext = null)
    {
        _serviceProvider = serviceProvider;
        _scopeContext = scopeContext;

        if (_scopeContext?.IsSystem == true)
        {
            SetBypassFilter(true);
        }
    }

    /// <inheritdoc/>
    public DefaultId? TenantId => _scopedTenantId ?? AmbientTenantId.Value;

    /// <inheritdoc/>
    public bool HasTenant => TenantId.HasValue && TenantId.Value != default;

    /// <inheritdoc/>
    public bool IsFilterBypassed => ShouldBypassFilter();

    /// <inheritdoc/>
    public void SetTenantId(DefaultId? tenantId)
    {
        _scopedTenantId = tenantId;
        AmbientTenantId.Value = tenantId;
        _cachedTenant = null;
        _tenantLoaded = false;
    }

    /// <inheritdoc/>
    public async Task<Tenant?> GetTenantAsync(CancellationToken ct = default)
    {
        if (_tenantLoaded)
        {
            return _cachedTenant;
        }

        var id = TenantId;
        if (!id.HasValue || id.Value == default)
        {
            _tenantLoaded = true;
            _cachedTenant = null;
            return null;
        }

        var dbContext = (TenantModuleDbContext?)_serviceProvider.GetService(typeof(TenantModuleDbContext));
        if (dbContext != null)
        {
            _cachedTenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value, ct);
        }

        _tenantLoaded = true;
        return _cachedTenant;
    }
}
