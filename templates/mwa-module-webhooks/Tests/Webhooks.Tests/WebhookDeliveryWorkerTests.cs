using System.Reflection;
using Kernel.Events;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Contracts.Events;
using Webhooks.Delivery;
using Webhooks.Entities;
using Webhooks.Persistence;
using Webhooks.Workers;
using Xunit;

namespace Webhooks.Tests;

public class WebhookDeliveryWorkerTests
{
    private class ConfigurableDeliveryEngine : IWebhookDeliveryEngine
    {
        public bool ShouldSucceed { get; set; } = true;
        public int CallsCount { get; set; }

        public Task<WebhookDelivery> DeliverAsync(WebhookSubscription subscription, WebhookEvent webhookEvent, int attemptNumber, CancellationToken ct = default)
        {
            CallsCount++;

            return Task.FromResult(new WebhookDelivery
            {
                Id = DefaultId.NewUlid(),
                SubscriptionId = subscription.Id,
                WebhookEventId = webhookEvent.Id,
                EventType = webhookEvent.EventType,
                Status = ShouldSucceed ? DeliveryStatus.Succeeded : DeliveryStatus.Failed,
                ResponseStatusCode = ShouldSucceed ? 200 : 500,
                ResponseBody = ShouldSucceed ? "{\"ok\":true}" : "Internal error",
                RequestBody = "{}",
                Error = ShouldSucceed ? null : "HTTP 500",
                AttemptNumber = attemptNumber,
                DurationMs = 20,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Subscription = subscription
            });
        }
    }

    private readonly WebhookModuleDbContext _dbContext;
    private readonly ConfigurableDeliveryEngine _deliveryEngine;
    private readonly FakeEventBus _eventBus;
    private readonly JsonSerializer _jsonSerializer;
    private readonly WebhookSettings _settings;
    private readonly ServiceProvider _serviceProvider;
    private readonly WebhookDeliveryWorker _worker;

    public WebhookDeliveryWorkerTests()
    {
        var options = new DbContextOptionsBuilder<WebhookModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new WebhookModuleDbContext(options);
        _deliveryEngine = new ConfigurableDeliveryEngine();
        _eventBus = new FakeEventBus();
        _jsonSerializer = new JsonSerializer();
        _settings = new WebhookSettings(_jsonSerializer);

        var services = new ServiceCollection();
        services.AddSingleton<WebhookModuleDbContext>(_dbContext);
        services.AddSingleton<IWebhookDeliveryEngine>(_deliveryEngine);
        services.AddSingleton<IEventBus>(_eventBus);
        services.AddSingleton<IJsonSerializer>(_jsonSerializer);
        services.AddSingleton<WebhookSettings>(_settings);

        _serviceProvider = services.BuildServiceProvider();

        _worker = new WebhookDeliveryWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _settings,
            NullLogger<WebhookDeliveryWorker>.Instance);
    }

    private async Task InvokeWorkerIterationAsync()
    {
        var method = typeof(WebhookDeliveryWorker).GetMethod("ExecuteJobAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method.Invoke(_worker, [_serviceProvider, CancellationToken.None])!;
        await task;
    }

    [Fact]
    public async Task Worker_FansOutEventsToMatchingSubscriptions()
    {
        // Setup 2 subscriptions: one wildcard, one specific
        var subAll = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "All Events",
            Url = "https://example.com/all",
            Secret = "secret",
            Status = "Active",
            OwnerUserId = DefaultId.NewUlid()
        };

        var subFiltered = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Filtered",
            Url = "https://example.com/filtered",
            Secret = "secret",
            EventTypes = "OrderCompletedEvent,PaymentReceivedEvent",
            Status = "Active",
            OwnerUserId = DefaultId.NewUlid()
        };

        var subPaused = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Paused",
            Url = "https://example.com/paused",
            Secret = "secret",
            Status = "Paused",
            OwnerUserId = DefaultId.NewUlid()
        };

        _dbContext.Subscriptions.AddRange(subAll, subFiltered, subPaused);

        // Add 2 events: one that matches both, one that only matches subAll
        var event1 = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "OrderCompletedEvent",
            Payload = "{}",
            IsProcessed = false,
            OccurredAt = DateTime.UtcNow
        };

        var event2 = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}",
            IsProcessed = false,
            OccurredAt = DateTime.UtcNow
        };

        _dbContext.Events.AddRange(event1, event2);
        await _dbContext.SaveChangesAsync();

        // Run worker
        await InvokeWorkerIterationAsync();

        // Verify events marked processed
        var reloadedEvent1 = await _dbContext.Events.FindAsync(event1.Id);
        var reloadedEvent2 = await _dbContext.Events.FindAsync(event2.Id);
        Assert.True(reloadedEvent1!.IsProcessed);
        Assert.True(reloadedEvent2!.IsProcessed);

        // Deliveries created:
        // event1 -> subAll, subFiltered (2 deliveries)
        // event2 -> subAll (1 delivery)
        // subPaused -> 0 deliveries
        var deliveries = await _dbContext.Deliveries.ToListAsync();
        Assert.Equal(3, deliveries.Count);

        // In the same run, pending deliveries are executed by deliverEngine
        Assert.All(deliveries, d => Assert.Equal(DeliveryStatus.Succeeded, d.Status));
    }

    [Fact]
    public async Task Worker_OnSuccessfulDelivery_UpdatesStatsAndPublishesSuccessEvent()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Sub",
            Url = "https://example.com",
            Secret = "secret",
            Status = "Active",
            ConsecutiveFailures = 3,
            TotalDeliveries = 10,
            OwnerUserId = DefaultId.NewUlid()
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}",
            IsProcessed = false,
            OccurredAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(sub);
        _dbContext.Events.Add(webhookEvent);
        await _dbContext.SaveChangesAsync();

        _deliveryEngine.ShouldSucceed = true;
        await InvokeWorkerIterationAsync();

        var reloadedSub = await _dbContext.Subscriptions.FindAsync(sub.Id);
        Assert.Equal(0, reloadedSub!.ConsecutiveFailures);
        Assert.Equal(11, reloadedSub.TotalDeliveries);
        Assert.NotNull(reloadedSub.LastDeliveryAt);

        Assert.Contains(_eventBus.PublishedEvents, e => e is WebhookDeliverySucceededEvent);
    }

    [Fact]
    public async Task Worker_OnFailedDelivery_CalculatesBackoffAndIncrementsFailures()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Sub",
            Url = "https://example.com",
            Secret = "secret",
            Status = "Active",
            ConsecutiveFailures = 0,
            TotalFailures = 2,
            OwnerUserId = DefaultId.NewUlid()
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}",
            IsProcessed = true,
            OccurredAt = DateTime.UtcNow
        };

        var delivery = new WebhookDelivery
        {
            Id = DefaultId.NewUlid(),
            SubscriptionId = sub.Id,
            WebhookEventId = webhookEvent.Id,
            EventType = webhookEvent.EventType,
            Status = DeliveryStatus.Pending,
            AttemptNumber = 1,
            RequestBody = "{}",
            CreatedAt = DateTime.UtcNow,
            Subscription = sub
        };

        _dbContext.Subscriptions.Add(sub);
        _dbContext.Events.Add(webhookEvent);
        _dbContext.Deliveries.Add(delivery);
        await _dbContext.SaveChangesAsync();

        _deliveryEngine.ShouldSucceed = false;
        await InvokeWorkerIterationAsync();

        var reloadedSub = await _dbContext.Subscriptions.FindAsync(sub.Id);
        Assert.Equal(1, reloadedSub!.ConsecutiveFailures);
        Assert.Equal(3, reloadedSub.TotalFailures);

        var reloadedDelivery = await _dbContext.Deliveries.FindAsync(delivery.Id);
        Assert.Equal(DeliveryStatus.Failed, reloadedDelivery!.Status);
        Assert.Equal(2, reloadedDelivery.AttemptNumber);
        Assert.NotNull(reloadedDelivery.NextRetryAt);
        Assert.True(reloadedDelivery.NextRetryAt > DateTime.UtcNow);

        Assert.Contains(_eventBus.PublishedEvents, e => e is WebhookDeliveryFailedEvent);
    }

    [Fact]
    public async Task Worker_AutoDisablesSubscription_WhenConsecutiveFailuresExceeded()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Sub",
            Url = "https://example.com",
            Secret = "secret",
            Status = "Active",
            ConsecutiveFailures = _settings.MaxConsecutiveFailures - 1,
            OwnerUserId = DefaultId.NewUlid()
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}",
            IsProcessed = true,
            OccurredAt = DateTime.UtcNow
        };

        var delivery = new WebhookDelivery
        {
            Id = DefaultId.NewUlid(),
            SubscriptionId = sub.Id,
            WebhookEventId = webhookEvent.Id,
            EventType = webhookEvent.EventType,
            Status = DeliveryStatus.Pending,
            AttemptNumber = 1,
            RequestBody = "{}",
            CreatedAt = DateTime.UtcNow,
            Subscription = sub
        };

        _dbContext.Subscriptions.Add(sub);
        _dbContext.Events.Add(webhookEvent);
        _dbContext.Deliveries.Add(delivery);
        await _dbContext.SaveChangesAsync();

        _deliveryEngine.ShouldSucceed = false;
        await InvokeWorkerIterationAsync();

        var reloadedSub = await _dbContext.Subscriptions.FindAsync(sub.Id);
        Assert.Equal("Disabled", reloadedSub!.Status);
        Assert.Contains(_eventBus.PublishedEvents, e => e is WebhookSubscriptionPausedEvent);
    }

    [Fact]
    public async Task Worker_DeadLettersDelivery_WhenMaxRetriesExhausted()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Sub",
            Url = "https://example.com",
            Secret = "secret",
            Status = "Active",
            OwnerUserId = DefaultId.NewUlid()
        };

        var webhookEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UserCreatedEvent",
            Payload = "{}",
            IsProcessed = true,
            OccurredAt = DateTime.UtcNow
        };

        var delivery = new WebhookDelivery
        {
            Id = DefaultId.NewUlid(),
            SubscriptionId = sub.Id,
            WebhookEventId = webhookEvent.Id,
            EventType = webhookEvent.EventType,
            Status = DeliveryStatus.Failed,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1), // Due
            AttemptNumber = _settings.MaxRetries, // Final attempt
            RequestBody = "{}",
            CreatedAt = DateTime.UtcNow,
            Subscription = sub
        };

        _dbContext.Subscriptions.Add(sub);
        _dbContext.Events.Add(webhookEvent);
        _dbContext.Deliveries.Add(delivery);
        await _dbContext.SaveChangesAsync();

        _deliveryEngine.ShouldSucceed = false;
        await InvokeWorkerIterationAsync();

        var reloadedDelivery = await _dbContext.Deliveries.FindAsync(delivery.Id);
        Assert.Equal(DeliveryStatus.DeadLettered, reloadedDelivery!.Status);
        Assert.Null(reloadedDelivery.NextRetryAt);
        Assert.NotNull(reloadedDelivery.CompletedAt);

        Assert.Contains(_eventBus.PublishedEvents, e => e is WebhookDeliveryDeadLetteredEvent);
    }
}
