using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to test connectivity and signature validation by delivering a test event.
/// </summary>
public record TestSubscription(DefaultId Id) : IRequest<WebhookDeliveryDetailDto>;

[Logging(Order = 1)]
public class TestSubscriptionHandler : IRequestHandler<TestSubscription, WebhookDeliveryDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public TestSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookDeliveryDetailDto>> HandleAsync(TestSubscription request, CancellationToken cancellationToken = default)
    {
        return _webhookService.TestSubscriptionAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
