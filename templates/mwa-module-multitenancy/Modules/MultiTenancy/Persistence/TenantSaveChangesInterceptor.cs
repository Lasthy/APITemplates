using Kernel.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MultiTenancy.Contracts.Entities;
using MultiTenancy.Services;

namespace MultiTenancy.Persistence;

/// <summary>
/// SaveChanges interceptor that automatically stamps <see cref="ITenantScoped.TenantId"/> on newly
/// added entities from the current <see cref="ITenantContext"/> and prevents saving scoped entities without a tenant.
/// </summary>
public class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IScopeContext _scopeContext;

    public TenantSaveChangesInterceptor(ITenantContext tenantContext, IScopeContext scopeContext)
    {
        _tenantContext = tenantContext;
        _scopeContext = scopeContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampTenantIds(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampTenantIds(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampTenantIds(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker.Entries<ITenantScoped>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (entries.Count == 0) return;

        var currentTenantId = _tenantContext.TenantId;
        var hasTenant = _tenantContext.HasTenant && currentTenantId.HasValue && currentTenantId.Value != default;

        foreach (var entry in entries)
        {
            var isTenantSet = entry.Entity.TenantId != default;

            if (!isTenantSet)
            {
                if (!hasTenant)
                {
                    throw new InvalidOperationException(
                        $"Cannot save entity of type '{entry.Entity.GetType().Name}' implementing ITenantScoped without a valid TenantId when no tenant is active.");
                }

                entry.Entity.TenantId = currentTenantId!.Value;
            }
            else if (hasTenant && entry.Entity.TenantId != currentTenantId!.Value && !_scopeContext.IsSystem)
            {
                throw new InvalidOperationException(
                    $"Cannot save entity of type '{entry.Entity.GetType().Name}' with TenantId '{entry.Entity.TenantId}' because it does not match the active tenant '{currentTenantId.Value}'.");
            }
        }
    }
}
