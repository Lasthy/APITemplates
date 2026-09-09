using Kernel.Events;
using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Contracts.Events;
using Webhooks.Delivery;
using Webhooks.Entities;
using Webhooks.Persistence;

namespace Webhooks.Workers;

/// <summary>
/// Background worker that processes unprocessed domain events into subscription deliveries,
/// dispatches HTTP deliveries, handles exponential backoff retries, and auto-disables faulty subscriptions.
/// </summary>
public class WebhookDeliveryWorker : BaseWorker
{
    private readonly WebhookSettings _settings;
    private readonly ILogger<WebhookDeliveryWorker> _logger;

    public WebhookDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        WebhookSettings settings,
        ILogger<WebhookDeliveryWorker> logger)
        : base(scopeFactory, logger, TimeSpan.FromSeconds(Math.Max(1, settings.ProcessingIntervalSeconds)))
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.ProcessingIntervalSeconds));

        var dbContext = services.GetRequiredService<WebhookModuleDbContext>();
        var deliveryEngine = services.GetRequiredService<IWebhookDeliveryEngine>();
        var eventBus = services.GetRequiredService<IEventBus>();

        // 1. Fan-out unprocessed WebhookEvents to matching active subscriptions
        await ProcessUnprocessedEventsAsync(dbContext, cancellationToken);

        // 2. Process pending and retryable deliveries
        await ProcessPendingDeliveriesAsync(dbContext, deliveryEngine, eventBus, cancellationToken);
    }

    private async Task ProcessUnprocessedEventsAsync(WebhookModuleDbContext dbContext, CancellationToken ct)
    {
        var unprocessedEvents = await dbContext.Events
            .Where(e => !e.IsProcessed)
            .OrderBy(e => e.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        if (unprocessedEvents.Count == 0)
        {
            return;
        }

        var activeSubscriptions = await dbContext.Subscriptions
            .Where(s => s.Status == "Active")
            .ToListAsync(ct);

        foreach (var webhookEvent in unprocessedEvents)
        {
            foreach (var subscription in activeSubscriptions)
            {
                if (MatchesEventFilter(subscription.EventTypes, webhookEvent.EventType))
                {
                    var delivery = new WebhookDelivery
                    {
                        Id = DefaultId.NewUlid(),
                        SubscriptionId = subscription.Id,
                        WebhookEventId = webhookEvent.Id,
                        EventType = webhookEvent.EventType,
                        Status = DeliveryStatus.Pending,
                        AttemptNumber = 1,
                        RequestBody = string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        Subscription = subscription
                    };

                    dbContext.Deliveries.Add(delivery);
                }
            }

            webhookEvent.IsProcessed = true;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task ProcessPendingDeliveriesAsync(
        WebhookModuleDbContext dbContext,
        IWebhookDeliveryEngine deliveryEngine,
        IEventBus eventBus,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var dueDeliveries = await dbContext.Deliveries
            .Include(d => d.Subscription)
            .Where(d => d.Status == DeliveryStatus.Pending ||
                        (d.Status == DeliveryStatus.Failed && d.NextRetryAt != null && d.NextRetryAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(25)
            .ToListAsync(ct);

        if (dueDeliveries.Count == 0)
        {
            return;
        }

        var eventIds = dueDeliveries.Select(d => d.WebhookEventId).Distinct().ToList();
        var eventsMap = await dbContext.Events
            .Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        foreach (var delivery in dueDeliveries)
        {
            if (ct.IsCancellationRequested) break;

            var subscription = delivery.Subscription;
            if (subscription == null || subscription.DeletedAt != null || subscription.Status != "Active")
            {
                continue;
            }

            if (!eventsMap.TryGetValue(delivery.WebhookEventId, out var webhookEvent))
            {
                // Event row missing, cannot deliver
                delivery.Status = DeliveryStatus.DeadLettered;
                delivery.Error = "Source WebhookEvent missing";
                delivery.CompletedAt = DateTime.UtcNow;
                continue;
            }

            var deliveryResult = await deliveryEngine.DeliverAsync(
                subscription,
                webhookEvent,
                delivery.AttemptNumber,
                ct);

            delivery.ResponseStatusCode = deliveryResult.ResponseStatusCode;
            delivery.ResponseBody = deliveryResult.ResponseBody;
            delivery.RequestBody = deliveryResult.RequestBody;
            delivery.DurationMs = deliveryResult.DurationMs;
            delivery.Error = deliveryResult.Error;

            if (deliveryResult.Status == DeliveryStatus.Succeeded)
            {
                delivery.Status = DeliveryStatus.Succeeded;
                delivery.CompletedAt = DateTime.UtcNow;
                delivery.NextRetryAt = null;

                subscription.ConsecutiveFailures = 0;
                subscription.TotalDeliveries++;
                subscription.LastDeliveryAt = DateTime.UtcNow;

                await eventBus.PublishAsync(
                    new WebhookDeliverySucceededEvent(subscription.Id, delivery.Id, delivery.EventType), ct);
            }
            else
            {
                subscription.ConsecutiveFailures++;
                subscription.TotalFailures++;

                // Auto-disable if consecutive failures threshold reached
                if (subscription.ConsecutiveFailures >= _settings.MaxConsecutiveFailures)
                {
                    subscription.Status = "Disabled";
                    await eventBus.PublishAsync(
                        new WebhookSubscriptionPausedEvent(
                            subscription.Id,
                            $"Auto-disabled after {subscription.ConsecutiveFailures} consecutive failures."), ct);
                }

                if (delivery.AttemptNumber >= _settings.MaxRetries)
                {
                    delivery.Status = DeliveryStatus.DeadLettered;
                    delivery.CompletedAt = DateTime.UtcNow;
                    delivery.NextRetryAt = null;

                    await eventBus.PublishAsync(
                        new WebhookDeliveryDeadLetteredEvent(
                            subscription.Id, delivery.Id, delivery.EventType, delivery.AttemptNumber), ct);
                }
                else
                {
                    delivery.Status = DeliveryStatus.Failed;
                    delivery.AttemptNumber++;

                    var delaySeconds = CalculateBackoffDelay(delivery.AttemptNumber);
                    delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);

                    await eventBus.PublishAsync(
                        new WebhookDeliveryFailedEvent(
                            subscription.Id,
                            delivery.Id,
                            delivery.EventType,
                            deliveryResult.ResponseStatusCode,
                            deliveryResult.Error ?? "Delivery failed"), ct);
                }
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private static bool MatchesEventFilter(string? subscriptionFilters, string eventType)
    {
        if (string.IsNullOrWhiteSpace(subscriptionFilters))
        {
            return true; // Wildcard
        }

        var filters = subscriptionFilters.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return filters.Contains(eventType, StringComparer.OrdinalIgnoreCase);
    }

    private double CalculateBackoffDelay(int attempt)
    {
        // Attempt 2: InitialRetryDelaySeconds
        // Attempt 3: Initial * Multiplier^1
        // Attempt n: Initial * Multiplier^(attempt - 2)
        var delay = _settings.InitialRetryDelaySeconds * Math.Pow(_settings.BackoffMultiplier, Math.Max(0, attempt - 2));
        var maxDelay = _settings.MaxRetryDelayMinutes * 60.0;
        return Math.Min(delay, maxDelay);
    }
}
