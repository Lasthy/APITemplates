using System.Reflection;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Entities;
using Webhooks.Persistence;
using Webhooks.Workers;
using Xunit;

namespace Webhooks.Tests;

public class DeliveryCleanupWorkerTests
{
    private readonly WebhookModuleDbContext _dbContext;
    private readonly WebhookSettings _settings;
    private readonly ServiceProvider _serviceProvider;
    private readonly DeliveryCleanupWorker _worker;

    public DeliveryCleanupWorkerTests()
    {
        var options = new DbContextOptionsBuilder<WebhookModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new WebhookModuleDbContext(options);
        _settings = new WebhookSettings(new JsonSerializer());

        var services = new ServiceCollection();
        services.AddSingleton<WebhookModuleDbContext>(_dbContext);
        _serviceProvider = services.BuildServiceProvider();

        _worker = new DeliveryCleanupWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _settings,
            NullLogger<DeliveryCleanupWorker>.Instance);
    }

    private async Task InvokeCleanupAsync()
    {
        var method = typeof(DeliveryCleanupWorker).GetMethod("ExecuteJobAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method.Invoke(_worker, [_serviceProvider, CancellationToken.None])!;
        await task;
    }

    [Fact]
    public async Task CleanupWorker_PurgesOldDeliveriesAndProcessedEvents()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Sub",
            Url = "https://example.com",
            Secret = "secret",
            OwnerUserId = DefaultId.NewUlid()
        };
        _dbContext.Subscriptions.Add(sub);

        var oldDate = DateTime.UtcNow.AddDays(-(_settings.CleanupRetentionDays + 5));
        var recentDate = DateTime.UtcNow.AddDays(-2);

        // 1. Old delivery -> should be deleted
        var oldDelivery = new WebhookDelivery
        {
            Id = DefaultId.NewUlid(),
            SubscriptionId = sub.Id,
            WebhookEventId = DefaultId.NewUlid(),
            EventType = "OldEvent",
            Status = DeliveryStatus.Succeeded,
            CreatedAt = oldDate,
            Subscription = sub
        };

        // 2. Recent delivery -> should be kept
        var recentDelivery = new WebhookDelivery
        {
            Id = DefaultId.NewUlid(),
            SubscriptionId = sub.Id,
            WebhookEventId = DefaultId.NewUlid(),
            EventType = "RecentEvent",
            Status = DeliveryStatus.Succeeded,
            CreatedAt = recentDate,
            Subscription = sub
        };

        // 3. Old processed event -> should be deleted
        var oldProcessedEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "OldEvent",
            Payload = "{}",
            IsProcessed = true,
            OccurredAt = oldDate
        };

        // 4. Old UNPROCESSED event -> should be kept!
        var oldUnprocessedEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "UnprocessedEvent",
            Payload = "{}",
            IsProcessed = false,
            OccurredAt = oldDate
        };

        // 5. Recent processed event -> should be kept
        var recentProcessedEvent = new WebhookEvent
        {
            Id = DefaultId.NewUlid(),
            EventType = "RecentEvent",
            Payload = "{}",
            IsProcessed = true,
            OccurredAt = recentDate
        };

        _dbContext.Deliveries.AddRange(oldDelivery, recentDelivery);
        _dbContext.Events.AddRange(oldProcessedEvent, oldUnprocessedEvent, recentProcessedEvent);
        await _dbContext.SaveChangesAsync();

        await InvokeCleanupAsync();

        Assert.Null(await _dbContext.Deliveries.FindAsync(oldDelivery.Id));
        Assert.NotNull(await _dbContext.Deliveries.FindAsync(recentDelivery.Id));

        Assert.Null(await _dbContext.Events.FindAsync(oldProcessedEvent.Id));
        Assert.NotNull(await _dbContext.Events.FindAsync(oldUnprocessedEvent.Id));
        Assert.NotNull(await _dbContext.Events.FindAsync(recentProcessedEvent.Id));
    }
}
