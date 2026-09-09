using Kernel.Common;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Contracts.Events;
using Webhooks.Delivery;
using Webhooks.Entities;
using Webhooks.Persistence;
using Webhooks.Services;
using Xunit;

namespace Webhooks.Tests;

public class WebhookServiceTests
{
    private readonly WebhookModuleDbContext _dbContext;
    private readonly FakeDeliveryEngine _deliveryEngine;
    private readonly FakeEventBus _eventBus;
    private readonly JsonSerializer _jsonSerializer;
    private readonly WebhookSettings _settings;
    private readonly WebhookService _service;

    private class FakeDeliveryEngine : IWebhookDeliveryEngine
    {
        public Task<WebhookDelivery> DeliverAsync(WebhookSubscription subscription, WebhookEvent webhookEvent, int attemptNumber, CancellationToken ct = default)
        {
            return Task.FromResult(new WebhookDelivery
            {
                Id = DefaultId.NewUlid(),
                SubscriptionId = subscription.Id,
                WebhookEventId = webhookEvent.Id,
                EventType = webhookEvent.EventType,
                Status = DeliveryStatus.Succeeded,
                ResponseStatusCode = 200,
                ResponseBody = "{\"ok\":true}",
                RequestBody = "{}",
                AttemptNumber = attemptNumber,
                DurationMs = 45,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Subscription = subscription
            });
        }
    }

    public WebhookServiceTests()
    {
        var options = new DbContextOptionsBuilder<WebhookModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new WebhookModuleDbContext(options);
        _deliveryEngine = new FakeDeliveryEngine();
        _eventBus = new FakeEventBus();
        _jsonSerializer = new JsonSerializer();
        _settings = new WebhookSettings(_jsonSerializer);

        _service = new WebhookService(_dbContext, _deliveryEngine, _eventBus, _jsonSerializer, _settings);
    }

    [Fact]
    public async Task CreateSubscription_GeneratesSecretAndPublishesEvent()
    {
        var ownerId = DefaultId.NewUlid();
        var request = new CreateSubscriptionRequest("My Hook", "https://example.com/webhook", "UserCreatedEvent");

        var result = await _service.CreateSubscriptionAsync(request, ownerId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("My Hook", result.Value.Name);
        Assert.Equal("https://example.com/webhook", result.Value.Url);
        Assert.Equal("Active", result.Value.Status);
        Assert.NotNull(result.Value.Secret);
        Assert.NotEmpty(result.Value.Secret);

        // Event was published
        Assert.Single(_eventBus.PublishedEvents);
        var createdEvent = Assert.IsType<WebhookSubscriptionCreatedEvent>(_eventBus.PublishedEvents[0]);
        Assert.Equal(result.Value.Id, createdEvent.SubscriptionId);
    }

    [Fact]
    public async Task CreateSubscription_EnforcesHttps_WhenConfigured()
    {
        var ownerId = DefaultId.NewUlid();
        var request = new CreateSubscriptionRequest("Insecure Hook", "http://example.com/webhook");

        var result = await _service.CreateSubscriptionAsync(request, ownerId);

        Assert.True(result.IsFailure);
        Assert.Contains("HTTPS", result.Error!.Message);
    }

    [Theory]
    [InlineData("https://localhost/webhook")]
    [InlineData("https://127.0.0.1/webhook")]
    [InlineData("https://10.0.1.5/webhook")]
    [InlineData("https://192.168.1.100/webhook")]
    [InlineData("https://172.16.5.10/webhook")]
    [InlineData("https://service.internal/webhook")]
    public async Task CreateSubscription_BlocksSsrfTargets(string targetUrl)
    {
        var ownerId = DefaultId.NewUlid();
        var request = new CreateSubscriptionRequest("SSRF Hook", targetUrl);

        var result = await _service.CreateSubscriptionAsync(request, ownerId);

        Assert.True(result.IsFailure);
        Assert.Contains("blocked", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSubscription_EnforcesMaxSubscriptionsPerUser()
    {
        var ownerId = DefaultId.NewUlid();
        var quota = _settings.MaxSubscriptionsPerUser;

        for (int i = 0; i < quota; i++)
        {
            var r = await _service.CreateSubscriptionAsync(
                new CreateSubscriptionRequest($"Hook {i}", "https://example.com/webhook"), ownerId);
            Assert.True(r.IsSuccess);
        }

        // Next subscription exceeds limit
        var exceeded = await _service.CreateSubscriptionAsync(
            new CreateSubscriptionRequest("Over Limit", "https://example.com/webhook"), ownerId);

        Assert.True(exceeded.IsFailure);
        Assert.Contains("limit", exceeded.Error!.Message);
    }

    [Fact]
    public async Task GetSubscriptions_IsolatesNonSystemUsers()
    {
        var user1 = DefaultId.NewUlid();
        var user2 = DefaultId.NewUlid();

        await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("User1 Hook", "https://example.com/1"), user1);
        await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("User2 Hook", "https://example.com/2"), user2);

        var user1Subs = await _service.GetSubscriptionsAsync(user1, isSystem: false);
        Assert.True(user1Subs.IsSuccess);
        Assert.Single(user1Subs.Value!);
        Assert.Equal("User1 Hook", user1Subs.Value![0].Name);

        var systemSubs = await _service.GetSubscriptionsAsync(null, isSystem: true);
        Assert.True(systemSubs.IsSuccess);
        Assert.Equal(2, systemSubs.Value!.Count);
    }

    [Fact]
    public async Task GetSubscription_PreventsUnauthorizedAccess()
    {
        var owner = DefaultId.NewUlid();
        var intruder = DefaultId.NewUlid();

        var created = await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("Secret Hook", "https://example.com/hook"), owner);
        var subId = created.Value!.Id;

        // Intruder gets denied
        var forbiddenResult = await _service.GetSubscriptionAsync(subId, intruder, isSystem: false);
        Assert.True(forbiddenResult.IsFailure);
        Assert.IsType<UnauthorizedAccessException>(forbiddenResult.Error);

        // Owner gets success
        var ownerResult = await _service.GetSubscriptionAsync(subId, owner, isSystem: false);
        Assert.True(ownerResult.IsSuccess);

        // System gets success
        var systemResult = await _service.GetSubscriptionAsync(subId, null, isSystem: true);
        Assert.True(systemResult.IsSuccess);
    }

    [Fact]
    public async Task PauseAndResumeSubscription_UpdatesStatusAndPublishesEvent()
    {
        var owner = DefaultId.NewUlid();
        var created = await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("Hook", "https://example.com/hook"), owner);
        var subId = created.Value!.Id;

        _eventBus.Clear();

        // Pause
        var pauseResult = await _service.PauseSubscriptionAsync(subId, owner, false);
        Assert.True(pauseResult.IsSuccess);

        var pausedSub = await _service.GetSubscriptionAsync(subId, owner, false);
        Assert.Equal("Paused", pausedSub.Value!.Status);
        Assert.Single(_eventBus.PublishedEvents);
        Assert.IsType<WebhookSubscriptionPausedEvent>(_eventBus.PublishedEvents[0]);

        // Resume
        var resumeResult = await _service.ResumeSubscriptionAsync(subId, owner, false);
        Assert.True(resumeResult.IsSuccess);

        var resumedSub = await _service.GetSubscriptionAsync(subId, owner, false);
        Assert.Equal("Active", resumedSub.Value!.Status);
    }

    [Fact]
    public async Task RotateSecret_GeneratesNewSecret()
    {
        var owner = DefaultId.NewUlid();
        var created = await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("Hook", "https://example.com/hook"), owner);
        var subId = created.Value!.Id;
        var initialSecret = created.Value!.Secret;

        var rotateResult = await _service.RotateSecretAsync(subId, owner, false);
        Assert.True(rotateResult.IsSuccess);
        Assert.NotNull(rotateResult.Value);
        Assert.NotEqual(initialSecret, rotateResult.Value);

        var dbSub = await _dbContext.Subscriptions.FindAsync(subId);
        Assert.Equal(rotateResult.Value, dbSub!.Secret);
    }

    [Fact]
    public async Task DeleteSubscription_SoftDeletesRecord()
    {
        var owner = DefaultId.NewUlid();
        var created = await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("Hook", "https://example.com/hook"), owner);
        var subId = created.Value!.Id;

        var deleteResult = await _service.DeleteSubscriptionAsync(subId, owner, false);
        Assert.True(deleteResult.IsSuccess);

        // Soft-deleted record is no longer returned by queries
        var getResult = await _service.GetSubscriptionAsync(subId, owner, false);
        Assert.True(getResult.IsFailure);
        Assert.IsType<KeyNotFoundException>(getResult.Error);
    }

    [Fact]
    public async Task TestSubscription_ExecutesDeliveryEngineAndStoresRecord()
    {
        var owner = DefaultId.NewUlid();
        var created = await _service.CreateSubscriptionAsync(new CreateSubscriptionRequest("Hook", "https://example.com/hook"), owner);
        var subId = created.Value!.Id;

        var testResult = await _service.TestSubscriptionAsync(subId, owner, false);

        Assert.True(testResult.IsSuccess);
        Assert.NotNull(testResult.Value);
        Assert.Equal(DeliveryStatus.Succeeded, testResult.Value.Status);
        Assert.Equal(200, testResult.Value.ResponseStatusCode);

        // Stored in database
        Assert.Equal(1, await _dbContext.Deliveries.CountAsync(d => d.SubscriptionId == subId));
        Assert.Equal(1, await _dbContext.Events.CountAsync());
    }
}
