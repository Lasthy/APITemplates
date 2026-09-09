using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Query to get detailed information about a single webhook subscription.
/// </summary>
public record GetSubscription(DefaultId Id) : IRequest<WebhookSubscriptionDetailDto>;

[Logging(Order = 1)]
public class GetSubscriptionHandler : IRequestHandler<GetSubscription, WebhookSubscriptionDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public GetSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookSubscriptionDetailDto>> HandleAsync(GetSubscription request, CancellationToken cancellationToken = default)
    {
        return _webhookService.GetSubscriptionAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
