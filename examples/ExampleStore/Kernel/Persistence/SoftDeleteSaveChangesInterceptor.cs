using Kernel.Context;
using Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kernel.Persistence;

/// <summary>
/// Interceptor that converts hard deletes on entities implementing <see cref="ISoftDeletable"/>
/// into soft deletes by changing the entity state to <see cref="EntityState.Modified"/> and
/// setting <see cref="ISoftDeletable.DeletedAt"/> and <see cref="ISoftDeletable.DeletedBy"/>.
/// </summary>
public class SoftDeleteSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IScopeContext? _scopeContext;

    public SoftDeleteSaveChangesInterceptor(IScopeContext? scopeContext = null)
    {
        _scopeContext = scopeContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftDelete(DbContext? context)
    {
        if (context == null) return;

        var utcNow = DateTime.UtcNow;
        var userId = _scopeContext?.UserId;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = utcNow;
                entry.Entity.DeletedBy = userId;
            }
        }
    }
}

