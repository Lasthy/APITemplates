using Kernel.Boxing;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Events;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Messaging.Workers;
using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Shopping.Tests;

public record SampleInboxTestEvent(string Message) : Event;

public class InboxTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public InboxTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InboxEntry_Initialization_SetsPropertiesCorrectly()
    {
        var messageId = DefaultId.NewUlid();
        var entryId = DefaultId.NewUlid();
        var now = DateTime.UtcNow;

        var entry = new InboxEntry
        {
            Id = entryId,
            MessageId = messageId,
            ProcessedAt = now
        };

        Assert.Equal(entryId, entry.Id);
        Assert.Equal(messageId, entry.MessageId);
        Assert.Equal(now, entry.ProcessedAt);
    }

    [Fact]
    public async Task Inbox_HasBeenProcessedAsync_ReturnsFalseForUnprocessedMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();

        var randomMessageId = DefaultId.NewUlid();
        var processed = await inbox.HasBeenProcessedAsync(randomMessageId);

        Assert.False(processed);
    }

    [Fact]
    public async Task Inbox_MarkProcessedAsync_RecordsMessageAndMakesItProcessed()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

        var messageId = DefaultId.NewUlid();

        Assert.False(await inbox.HasBeenProcessedAsync(messageId));

        await inbox.MarkProcessedAsync(messageId);

        Assert.True(await inbox.HasBeenProcessedAsync(messageId));

        var dbEntry = await db.InboxEntries.FirstOrDefaultAsync(e => e.MessageId == messageId);
        Assert.NotNull(dbEntry);
        Assert.Equal(messageId, dbEntry.MessageId);
        Assert.True(dbEntry.ProcessedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Inbox_MarkProcessedAsync_IsIdempotentOnDuplicateCalls()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

        var messageId = DefaultId.NewUlid();

        // Calling multiple times should not throw
        await inbox.MarkProcessedAsync(messageId);
        await inbox.MarkProcessedAsync(messageId);
        await inbox.MarkProcessedAsync(messageId);

        Assert.True(await inbox.HasBeenProcessedAsync(messageId));

        var matchingEntries = await db.InboxEntries.Where(e => e.MessageId == messageId).ToListAsync();
        Assert.Single(matchingEntries);
    }

    [Fact]
    public async Task EventMessageProcessor_ProcessesMessageAndRecordsInInbox()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
        var registry = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();

        try { registry.Register<SampleInboxTestEvent>(); } catch { /* Ignore if already registered */ }

        var fakeEventBus = new TestEventBus();
        var processor = new EventMessageProcessor(
            fakeEventBus,
            registry,
            jsonSerializer,
            inbox,
            NullLogger<EventMessageProcessor>.Instance);

        var testEvent = new SampleInboxTestEvent("Test content");
        var message = new Message
        {
            Id = DefaultId.NewUlid(),
            Type = typeof(SampleInboxTestEvent).FullName!,
            Content = jsonSerializer.Serialize(testEvent),
            Channel = MessageChannel.Event,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsSuccess, result.Error?.ToString() ?? "Success expected");
        Assert.Single(fakeEventBus.PublishedEvents);

        // Verify recorded in inbox
        var isProcessed = await inbox.HasBeenProcessedAsync(message.Id);
        Assert.True(isProcessed);
    }

    [Fact]
    public async Task EventMessageProcessor_SkipsDuplicateProcessing_WhenAlreadyInInbox()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
        var registry = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();

        try { registry.Register<SampleInboxTestEvent>(); } catch { /* Ignore if already registered */ }

        var fakeEventBus = new TestEventBus();
        var processor = new EventMessageProcessor(
            fakeEventBus,
            registry,
            jsonSerializer,
            inbox,
            NullLogger<EventMessageProcessor>.Instance);

        var testEvent = new SampleInboxTestEvent("Idempotent event");
        var message = new Message
        {
            Id = DefaultId.NewUlid(),
            Type = typeof(SampleInboxTestEvent).FullName!,
            Content = jsonSerializer.Serialize(testEvent),
            Channel = MessageChannel.Event,
            Partition = 0
        };

        // First process — should succeed and publish
        var firstResult = await processor.ProcessMessage(message);
        Assert.True(firstResult.IsSuccess);
        Assert.Single(fakeEventBus.PublishedEvents);

        // Second process with same message — should be skipped due to inbox idempotency guard
        var secondResult = await processor.ProcessMessage(message);
        Assert.True(secondResult.IsSuccess);
        // Event should NOT have been published a second time
        Assert.Single(fakeEventBus.PublishedEvents);
    }

    [Fact]
    public async Task EventMessageProcessor_DoesNotRecordInInbox_WhenDeserializationFails()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
        var registry = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();

        try { registry.Register<SampleInboxTestEvent>(); } catch { /* Ignore if already registered */ }

        var fakeEventBus = new TestEventBus();
        var processor = new EventMessageProcessor(
            fakeEventBus,
            registry,
            jsonSerializer,
            inbox,
            NullLogger<EventMessageProcessor>.Instance);

        var message = new Message
        {
            Id = DefaultId.NewUlid(),
            Type = typeof(SampleInboxTestEvent).FullName!,
            Content = "invalid-json-content",
            Channel = MessageChannel.Event,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsFailure);
        Assert.Empty(fakeEventBus.PublishedEvents);

        // Should NOT be marked as processed in inbox
        var isProcessed = await inbox.HasBeenProcessedAsync(message.Id);
        Assert.False(isProcessed);
    }

    [Fact]
    public async Task EventMessageProcessor_DoesNotRecordInInbox_WhenEventPublishingFails()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
        var registry = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();

        try { registry.Register<SampleInboxTestEvent>(); } catch { /* Ignore if already registered */ }

        var failingEventBus = new FailingEventBus();
        var processor = new EventMessageProcessor(
            failingEventBus,
            registry,
            jsonSerializer,
            inbox,
            NullLogger<EventMessageProcessor>.Instance);

        var testEvent = new SampleInboxTestEvent("Failing event");
        var message = new Message
        {
            Id = DefaultId.NewUlid(),
            Type = typeof(SampleInboxTestEvent).FullName!,
            Content = jsonSerializer.Serialize(testEvent),
            Channel = MessageChannel.Event,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsFailure);

        // Should NOT be marked as processed in inbox
        var isProcessed = await inbox.HasBeenProcessedAsync(message.Id);
        Assert.False(isProcessed);
    }

    [Fact]
    public async Task MessageCleanupWorker_CleansUpExpiredInboxEntries()
    {
        var oldMessageId = DefaultId.NewUlid();
        var recentMessageId = DefaultId.NewUlid();

        using (var setupScope = _factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<KernelDbContext>();
            var dynamicSettings = setupScope.ServiceProvider.GetRequiredService<DynamicSettings>();
            var messagingSettings = dynamicSettings.GetSection<MessagingSettings>();

            var oldEntry = new InboxEntry
            {
                Id = DefaultId.NewUlid(),
                MessageId = oldMessageId,
                ProcessedAt = DateTime.UtcNow.AddDays(-(messagingSettings.CleanupRetentionDays + 2))
            };

            var recentEntry = new InboxEntry
            {
                Id = DefaultId.NewUlid(),
                MessageId = recentMessageId,
                ProcessedAt = DateTime.UtcNow
            };

            await db.InboxEntries.AddRangeAsync(oldEntry, recentEntry);
            await db.SaveChangesAsync();
        }

        using (var workerScope = _factory.Services.CreateScope())
        {
            var dynamicSettings = workerScope.ServiceProvider.GetRequiredService<DynamicSettings>();
            var messagingSettings = dynamicSettings.GetSection<MessagingSettings>();
            var scopeFactory = workerScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            var worker = new TestMessageCleanupWorker(scopeFactory, NullLogger<MessageCleanupWorker>.Instance, messagingSettings);
            await worker.RunJobAsync(workerScope.ServiceProvider, CancellationToken.None);
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<KernelDbContext>();
            var oldInDb = await db.InboxEntries.AnyAsync(e => e.MessageId == oldMessageId);
            var recentInDb = await db.InboxEntries.AnyAsync(e => e.MessageId == recentMessageId);

            Assert.False(oldInDb, "Old inbox entry should have been deleted by MessageCleanupWorker.");
            Assert.True(recentInDb, "Recent inbox entry should be preserved.");
        }
    }

    private class TestEventBus : IEventBus
    {
        public List<IEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
        {
            PublishedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private class FailingEventBus : IEventBus
    {
        public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
        {
            throw new InvalidOperationException("Simulated event publishing failure");
        }
    }

    private class TestMessageCleanupWorker(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Logging.ILogger<MessageCleanupWorker> logger,
        MessagingSettings messagingSettings
    ) : MessageCleanupWorker(scopeFactory, logger, messagingSettings)
    {
        public Task RunJobAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return ExecuteJobAsync(services, cancellationToken);
        }
    }
}
