using Kernel.Entities;
using Kernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kernel.Persistence;

/// <summary>
/// Interceptor that gathers and dispatches domain events produced by entities implementing
/// <see cref="IHasDomainEvents"/> after changes have been successfully committed to the database.
/// </summary>
public class DispatchDomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IEventBus? _eventBus;

    public DispatchDomainEventsSaveChangesInterceptor(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        DispatchDomainEvents(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void DispatchDomainEvents(DbContext? context)
    {
        if (context == null || _eventBus == null) return;

        var domainEntities = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            _eventBus.PublishAsync(domainEvent).GetAwaiter().GetResult();
        }
    }

    private async Task DispatchDomainEventsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null || _eventBus == null) return;

        var domainEntities = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await _eventBus.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
