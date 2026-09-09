using Kernel.Events;
using Kernel.Json;
using Webhooks.Entities;
using Webhooks.Persistence;

namespace Webhooks.EventBridge;

/// <summary>
/// Event handler that intercepts all domain events inheriting from <see cref="Event"/>
/// and persists them as queued <see cref="WebhookEvent"/> entities for asynchronous delivery.
/// </summary>
public class WebhookEventBridge : IEventHandler<Event>
{
    private readonly WebhookModuleDbContext _dbContext;
    private readonly IJsonSerializer _jsonSerializer;

    public WebhookEventBridge(WebhookModuleDbContext dbContext, IJsonSerializer jsonSerializer)
    {
        _dbContext = dbContext;
        _jsonSerializer = jsonSerializer;
    }

    public async Task HandleAsync(Event @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = @event.GetType();

        // Prevent recursive loop by ignoring all events originated within Webhooks module events
        if (eventType.Namespace?.StartsWith("Webhooks.Contracts.Events", StringComparison.Ordinal) == true ||
            eventType.Namespace?.StartsWith("Webhooks.Events", StringComparison.Ordinal) == true ||
            eventType.Name.StartsWith("WebhookDelivery", StringComparison.Ordinal) ||
            eventType.Name.StartsWith("WebhookSubscription", StringComparison.Ordinal) ||
            eventType.Name.StartsWith("WebhookTest", StringComparison.Ordinal))
        {
            return;
        }

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = eventType.Name,
            Payload = _jsonSerializer.Serialize(@event),
            IsProcessed = false,
            OccurredAt = @event.OccurredAt != default ? @event.OccurredAt : DateTime.UtcNow
        };

        _dbContext.Events.Add(webhookEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
