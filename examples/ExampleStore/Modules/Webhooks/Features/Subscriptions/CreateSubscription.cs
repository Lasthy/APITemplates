using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to register a new webhook subscription.
/// </summary>
public record CreateSubscription(
    string Name,
    string Url,
    string? EventTypes = null,
    string? Description = null,
    string? CustomHeadersJson = null) : IRequest<WebhookSubscriptionDetailDto>;

[Logging(Order = 1)]
public class CreateSubscriptionHandler : IRequestHandler<CreateSubscription, WebhookSubscriptionDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public CreateSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookSubscriptionDetailDto>> HandleAsync(CreateSubscription request, CancellationToken cancellationToken = default)
    {
        var ownerUserId = _scopeContext.UserId ?? default;
        return _webhookService.CreateSubscriptionAsync(
            new CreateSubscriptionRequest(
                request.Name,
                request.Url,
                request.EventTypes,
                request.Description,
                request.CustomHeadersJson),
            ownerUserId,
            cancellationToken);
    }
}
