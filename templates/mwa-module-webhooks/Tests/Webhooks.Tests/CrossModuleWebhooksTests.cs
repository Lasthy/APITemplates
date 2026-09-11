using Microsoft.EntityFrameworkCore;
using Webhooks.Contracts.Requests;
using Webhooks.Entities;
using Webhooks.Features;
using Webhooks.Persistence;
using Xunit;

namespace Webhooks.Tests;

public class CrossModuleWebhooksTests
{
    private readonly WebhookModuleDbContext _dbContext;
    private readonly GetSubscriptionSummaryHandler _handler;

    public CrossModuleWebhooksTests()
    {
        var options = new DbContextOptionsBuilder<WebhookModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new WebhookModuleDbContext(options);
        _handler = new GetSubscriptionSummaryHandler(_dbContext);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSummaryForExistingSubscription()
    {
        var sub = new WebhookSubscription
        {
            Id = DefaultId.NewUlid(),
            Name = "Billing Sync",
            Url = "https://billing.example.com/webhooks",
            Secret = "secret",
            Status = "Active",
            EventTypes = "InvoicePaidEvent",
            OwnerUserId = DefaultId.NewUlid()
        };

        _dbContext.Subscriptions.Add(sub);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetSubscriptionSummary(sub.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sub.Id, result.Value.Id);
        Assert.Equal("Billing Sync", result.Value.Name);
        Assert.Equal("https://billing.example.com/webhooks", result.Value.Url);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal("InvoicePaidEvent", result.Value.EventTypes);
    }

    [Fact]
    public async Task HandleAsync_ReturnsFailureForNonExistentSubscription()
    {
        var nonExistentId = DefaultId.NewUlid();
        var result = await _handler.HandleAsync(new GetSubscriptionSummary(nonExistentId));

        Assert.True(result.IsFailure);
        Assert.IsType<KeyNotFoundException>(result.Error);
    }
}
