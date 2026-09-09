using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Deliveries;

/// <summary>
/// Query to retrieve complete details of a single webhook delivery attempt.
/// </summary>
public record GetDelivery(DefaultId Id) : IRequest<WebhookDeliveryDetailDto>;

[Logging(Order = 1)]
public class GetDeliveryHandler : IRequestHandler<GetDelivery, WebhookDeliveryDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public GetDeliveryHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookDeliveryDetailDto>> HandleAsync(GetDelivery request, CancellationToken cancellationToken = default)
    {
        return _webhookService.GetDeliveryAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
