using System.Collections.Concurrent;
using Kernel.Boxing;
using Kernel.Context;
using Kernel.Json;
using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.AuditLog;

/// <summary>
/// Interceptor that captures entity mutations (creates, updates, and deletes) across DbContexts
/// and persists structured audit log records with property-level diffs into <see cref="KernelDbContext"/>.
/// </summary>
public class AuditLogSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IScopeContext? _scopeContext;
    private readonly IJsonSerializer _jsonSerializer;

    private readonly ConcurrentDictionary<DbContext, List<AuditEntryDescriptor>> _pendingAudits = new();

    public AuditLogSaveChangesInterceptor(
        IServiceProvider? serviceProvider = null,
        IScopeContext? scopeContext = null,
        IJsonSerializer? jsonSerializer = null)
    {
        _serviceProvider = serviceProvider;
        _scopeContext = scopeContext;
        _jsonSerializer = jsonSerializer ?? new JsonSerializer();
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PrepareAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PrepareAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        PersistAuditEntries(eventData.Context, false, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PersistAuditEntries(eventData.Context, true, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null)
        {
            _pendingAudits.TryRemove(eventData.Context, out _);
        }
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            _pendingAudits.TryRemove(eventData.Context, out _);
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void PrepareAuditEntries(DbContext? context)
    {
        if (context == null) return;

        var descriptors = new List<AuditEntryDescriptor>();
        var utcNow = DateTime.UtcNow;
        var userId = _scopeContext?.UserId;
        var correlationId = _scopeContext?.CorrelationId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip audit entries themselves and internal outbox messages to avoid infinite recursion / noise
            if (entry.Entity is AuditEntry || entry.Entity is Message)
            {
                continue;
            }

            if (entry.State != EntityState.Added &&
                entry.State != EntityState.Modified &&
                entry.State != EntityState.Deleted)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => (AuditAction?)null
            };

            if (action == null) continue;

            var changes = new List<AuditPropertyChange>();
            var entityType = entry.Metadata.ClrType?.Name ?? entry.Entity.GetType().Name;

            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsShadowProperty()) continue;

                var propName = prop.Metadata.Name;

                if (entry.State == EntityState.Added)
                {
                    var current = prop.CurrentValue?.ToString();
                    changes.Add(new AuditPropertyChange(propName, null, current));
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (prop.IsModified)
                    {
                        var original = prop.OriginalValue?.ToString();
                        var current = prop.CurrentValue?.ToString();

                        if (!Equals(original, current))
                        {
                            changes.Add(new AuditPropertyChange(propName, original, current));
                        }
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var original = prop.OriginalValue?.ToString();
                    changes.Add(new AuditPropertyChange(propName, original, null));
                }
            }

            // For Modified state, if no properties actually changed, skip
            if (entry.State == EntityState.Modified && changes.Count == 0)
            {
                continue;
            }

            var entityId = GetEntityId(entry);

            descriptors.Add(new AuditEntryDescriptor(
                entry,
                entityType,
                entityId,
                action.Value,
                changes,
                userId,
                correlationId,
                utcNow
            ));
        }

        if (descriptors.Count > 0)
        {
            _pendingAudits[context] = descriptors;
        }
    }

    private async Task PersistAuditEntries(DbContext? context, bool async, CancellationToken cancellationToken)
    {
        if (context == null || !_pendingAudits.TryRemove(context, out var descriptors) || descriptors.Count == 0)
        {
            return;
        }

        var auditEntries = new List<AuditEntry>();

        foreach (var descriptor in descriptors)
        {
            var entityId = descriptor.EntityId;
            if (string.IsNullOrWhiteSpace(entityId) || entityId == "0")
            {
                entityId = GetEntityId(descriptor.Entry);
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                entityId = "Unknown";
            }

            var changesJson = descriptor.Changes.Count > 0
                ? _jsonSerializer.Serialize(descriptor.Changes)
                : null;

            auditEntries.Add(new AuditEntry
            {
                UserId = descriptor.UserId,
                EntityType = descriptor.EntityType,
                EntityId = entityId,
                Action = descriptor.Action,
                Changes = changesJson,
                CorrelationId = descriptor.CorrelationId,
                OccurredAt = descriptor.OccurredAt
            });
        }

        if (auditEntries.Count == 0) return;

        // Resolve KernelDbContext to persist the audit entries
        KernelDbContext? kernelDbContext = null;
        if (_serviceProvider != null)
        {
            kernelDbContext = _serviceProvider.GetService<KernelDbContext>();
        }

        if (kernelDbContext == null) return;

        kernelDbContext.AuditEntries.AddRange(auditEntries);

        if (async)
        {
            await kernelDbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            kernelDbContext.SaveChanges();
        }
    }

    private static string GetEntityId(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey != null && primaryKey.Properties.Count > 0)
        {
            var keyValues = primaryKey.Properties
                .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? entry.Property(p.Name).OriginalValue?.ToString() ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();

            if (keyValues.Length > 0)
            {
                return string.Join(",", keyValues);
            }
        }

        var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
        if (idProp != null)
        {
            return idProp.CurrentValue?.ToString() ?? idProp.OriginalValue?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private record AuditEntryDescriptor(
        EntityEntry Entry,
        string EntityType,
        string EntityId,
        AuditAction Action,
        List<AuditPropertyChange> Changes,
        DefaultId? UserId,
        string? CorrelationId,
        DateTime OccurredAt);
}
