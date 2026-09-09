using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Query to list webhook subscriptions for the current user or all if caller is System.
/// </summary>
public record GetSubscriptions : IRequest<IReadOnlyList<WebhookSubscriptionDto>>;

[Logging(Order = 1)]
public class GetSubscriptionsHandler : IRequestHandler<GetSubscriptions, IReadOnlyList<WebhookSubscriptionDto>>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public GetSubscriptionsHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<IReadOnlyList<WebhookSubscriptionDto>>> HandleAsync(GetSubscriptions request, CancellationToken cancellationToken = default)
    {
        return _webhookService.GetSubscriptionsAsync(_scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
