using Kernel.Events;
using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Contracts.Events;
using Webhooks.EventBridge;
using Webhooks.Persistence;
using Xunit;

namespace Webhooks.Tests;

public class WebhookEventBridgeTests
{
    private record SampleDomainEvent(string Name, decimal Amount) : Event;

    private readonly WebhookModuleDbContext _dbContext;
    private readonly JsonSerializer _jsonSerializer;
    private readonly WebhookEventBridge _bridge;

    public WebhookEventBridgeTests()
    {
        var options = new DbContextOptionsBuilder<WebhookModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new WebhookModuleDbContext(options);
        _jsonSerializer = new JsonSerializer();
        _bridge = new WebhookEventBridge(_dbContext, _jsonSerializer);
    }

    [Fact]
    public async Task HandleAsync_CapturesDomainEventIntoQueue()
    {
        var domainEvent = new SampleDomainEvent("Alice", 120.00m);

        await _bridge.HandleAsync(domainEvent);

        var queued = await _dbContext.Events.FirstOrDefaultAsync();
        Assert.NotNull(queued);
        Assert.Equal(nameof(SampleDomainEvent), queued.EventType);
        Assert.False(queued.IsProcessed);
        Assert.Contains("Alice", queued.Payload);
    }

    [Fact]
    public async Task HandleAsync_IgnoresWebhookInternalEvents_ToPreventLoops()
    {
        var subId = DefaultId.NewUlid();
        var deliveryId = DefaultId.NewUlid();

        await _bridge.HandleAsync(new WebhookDeliverySucceededEvent(subId, deliveryId, "SomeEvent"));
        await _bridge.HandleAsync(new WebhookDeliveryFailedEvent(subId, deliveryId, "SomeEvent", 500, "Error"));
        await _bridge.HandleAsync(new WebhookDeliveryDeadLetteredEvent(subId, deliveryId, "SomeEvent", 5));
        await _bridge.HandleAsync(new WebhookSubscriptionCreatedEvent(subId, "https://example.com", null));
        await _bridge.HandleAsync(new WebhookSubscriptionPausedEvent(subId, "Disabled"));
        await _bridge.HandleAsync(new WebhookTestEvent(subId, "Test", DateTime.UtcNow));

        var count = await _dbContext.Events.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task InProcessEventBus_DispatchesPolymorphicallyToEventBridge()
    {
        var services = new ServiceCollection();
        services.AddSingleton<WebhookModuleDbContext>(_dbContext);
        services.AddSingleton<IJsonSerializer>(_jsonSerializer);
        services.AddScoped<IEventHandler<Event>, WebhookEventBridge>();

        var provider = services.BuildServiceProvider();
        var bus = new InProcessEventBus(provider);

        var domainEvent = new SampleDomainEvent("Bob", 250.00m);
        await bus.PublishAsync(domainEvent);

        var queued = await _dbContext.Events.FirstOrDefaultAsync();
        Assert.NotNull(queued);
        Assert.Equal(nameof(SampleDomainEvent), queued.EventType);
        Assert.Contains("Bob", queued.Payload);
    }
}
