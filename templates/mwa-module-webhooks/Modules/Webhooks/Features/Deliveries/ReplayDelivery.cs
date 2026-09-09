using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Deliveries;

/// <summary>
/// Command to replay a failed or dead-lettered webhook delivery.
/// </summary>
public record ReplayDelivery(DefaultId Id) : IRequest<WebhookDeliveryDetailDto>;

[Logging(Order = 1)]
public class ReplayDeliveryHandler : IRequestHandler<ReplayDelivery, WebhookDeliveryDetailDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public ReplayDeliveryHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<WebhookDeliveryDetailDto>> HandleAsync(ReplayDelivery request, CancellationToken cancellationToken = default)
    {
        return _webhookService.ReplayDeliveryAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
