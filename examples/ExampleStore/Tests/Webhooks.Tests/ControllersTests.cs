using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Mvc;
using Webhooks.Contracts.Dtos;
using Webhooks.Controllers;
using Webhooks.Features.Deliveries;
using Webhooks.Features.Subscriptions;
using Xunit;
using static Webhooks.Controllers.SubscriptionsController;

namespace Webhooks.Tests;

public class ControllersTests
{
    private class FakeDispatcher : IDispatcher
    {
        public object? LastDispatchedRequest { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastDispatchedRequest = request;

            if (request is CreateSubscription cs)
            {
                var dto = new WebhookSubscriptionDetailDto(
                    DefaultId.NewUlid(), cs.Name, cs.Url, "Active", cs.EventTypes, cs.Description,
                    0, 0, 0, null, DateTime.UtcNow, "test-secret");
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetSubscriptions)
            {
                var list = new List<WebhookSubscriptionDto>
                {
                    new(DefaultId.NewUlid(), "Sub 1", "https://example.com/1", "Active", null, DateTime.UtcNow)
                };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetSubscription gs)
            {
                var dto = new WebhookSubscriptionDetailDto(
                    gs.Id, "Sub 1", "https://example.com/1", "Active", null, null,
                    0, 0, 0, null, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is UpdateSubscription us)
            {
                var dto = new WebhookSubscriptionDetailDto(
                    us.Id, us.Name ?? "Updated", us.Url ?? "https://example.com/updated", "Active", us.EventTypes, us.Description,
                    0, 0, 0, null, DateTime.UtcNow);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is DeleteSubscription or PauseSubscription or ResumeSubscription)
            {
                return Task.FromResult(Result.Success((TResponse)(object)Result.Success()));
            }

            if (request is RotateSecret)
            {
                return Task.FromResult(Result.Success((TResponse)(object)"newly-rotated-secret"));
            }

            if (request is TestSubscription ts)
            {
                var dto = new WebhookDeliveryDetailDto(
                    DefaultId.NewUlid(), ts.Id, "WebhookTestEvent", "Succeeded", 200, "{\"ok\":true}",
                    "{}", null, 1, 30, DateTime.UtcNow, DateTime.UtcNow, null);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetDeliveries)
            {
                var list = new List<WebhookDeliveryDto>
                {
                    new(DefaultId.NewUlid(), DefaultId.NewUlid(), "OrderCompleted", "Succeeded", 200, 1, 25, DateTime.UtcNow)
                };
                return Task.FromResult(Result.Success((TResponse)(object)list));
            }

            if (request is GetDelivery gd)
            {
                var dto = new WebhookDeliveryDetailDto(
                    gd.Id, DefaultId.NewUlid(), "OrderCompleted", "Succeeded", 200, "{}",
                    "{}", null, 1, 25, DateTime.UtcNow, DateTime.UtcNow, null);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is ReplayDelivery rd)
            {
                var dto = new WebhookDeliveryDetailDto(
                    rd.Id, DefaultId.NewUlid(), "OrderCompleted", "Succeeded", 200, "{}",
                    "{}", null, 2, 35, DateTime.UtcNow, DateTime.UtcNow, null);
                return Task.FromResult(Result.Success((TResponse)(object)dto));
            }

            if (request is GetDeliveryStats)
            {
                var stats = new DeliveryStatsDto(100, 95, 4, 1, 0, 42.5);
                return Task.FromResult(Result.Success((TResponse)(object)stats));
            }

            return Task.FromResult(Result.Failure<TResponse>(new NotImplementedException()));
        }
    }

    [Fact]
    public async Task SubscriptionsController_Create_ReturnsCreatedAtAction()
    {
        var dispatcher = new FakeDispatcher();
        var controller = new SubscriptionsController(dispatcher);

        var body = new CreateSubscriptionBody("Hook", "https://example.com/webhook", "UserCreatedEvent");
        var response = await controller.Create(body, CancellationToken.None);

        var actionResult = Assert.IsType<CreatedAtActionResult>(response.Result);
        var detail = Assert.IsType<WebhookSubscriptionDetailDto>(actionResult.Value);
        Assert.Equal("Hook", detail.Name);
        Assert.IsType<CreateSubscription>(dispatcher.LastDispatchedRequest);
    }

    [Fact]
    public async Task SubscriptionsController_GetAll_ReturnsOk()
    {
        var dispatcher = new FakeDispatcher();
        var controller = new SubscriptionsController(dispatcher);

        var response = await controller.GetAll(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<WebhookSubscriptionDto>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task SubscriptionsController_RotateSecret_ReturnsNewSecret()
    {
        var dispatcher = new FakeDispatcher();
        var controller = new SubscriptionsController(dispatcher);
        var id = DefaultId.NewUlid();

        var response = await controller.RotateSecret(id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task DeliveriesController_GetStats_ReturnsOkWithStats()
    {
        var dispatcher = new FakeDispatcher();
        var controller = new DeliveriesController(dispatcher);

        var response = await controller.GetStats(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var stats = Assert.IsType<DeliveryStatsDto>(okResult.Value);
        Assert.Equal(100, stats.TotalDeliveries);
        Assert.Equal(95, stats.Succeeded);
    }

    [Fact]
    public async Task DeliveriesController_Replay_ReturnsOkWithReplayDetails()
    {
        var dispatcher = new FakeDispatcher();
        var controller = new DeliveriesController(dispatcher);
        var id = DefaultId.NewUlid();

        var response = await controller.Replay(id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var delivery = Assert.IsType<WebhookDeliveryDetailDto>(okResult.Value);
        Assert.Equal(2, delivery.AttemptNumber);
    }
}
