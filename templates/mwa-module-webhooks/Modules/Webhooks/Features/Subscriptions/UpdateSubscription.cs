using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to update webhook subscription metadata or target URL.
/// </summary>
public record UpdateSubscription(
    DefaultId Id,
    string? Name = null,
    string? Url = null,
    string? EventTypes = null,
    string? Description = null,
    string? CustomHeadersJson = null) : IRequest<WebhookSubscriptionDetailDto>;

[Logging(Order = 1)]
public class UpdateSubscriptionHandler : IRequestHandler<UpdateSubscription, WebhookSubscriptionDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public UpdateSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookSubscriptionDetailDto>> HandleAsync(UpdateSubscription request, CancellationToken cancellationToken = default)
    {
        return _webhookService.UpdateSubscriptionAsync(
            request.Id,
            new UpdateSubscriptionRequest(
                request.Name,
                request.Url,
                request.EventTypes,
                request.Description,
                request.CustomHeadersJson),
            _scopeContext.UserId,
            _scopeContext.IsSystem,
            cancellationToken);
    }
}
