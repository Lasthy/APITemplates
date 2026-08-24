using Kernel.Context;
using Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kernel.Persistence;

/// <summary>
/// Interceptor that automatically populates audit fields on entities implementing <see cref="IAuditable"/>.
/// Sets <see cref="IAuditable.CreatedAt"/> and <see cref="IAuditable.CreatedBy"/> when added,
/// and <see cref="IAuditable.UpdatedAt"/> and <see cref="IAuditable.UpdatedBy"/> when modified.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IScopeContext? _scopeContext;

    public AuditSaveChangesInterceptor(IScopeContext? scopeContext = null)
    {
        _scopeContext = scopeContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context == null) return;

        var utcNow = DateTime.UtcNow;
        var userId = _scopeContext?.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = utcNow;
                }

                if (entry.Entity.CreatedBy == null)
                {
                    entry.Entity.CreatedBy = userId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }
}

