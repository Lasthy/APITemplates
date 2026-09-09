using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Deliveries;

/// <summary>
/// Query to compute aggregate statistics across deliveries.
/// </summary>
public record GetDeliveryStats : IRequest<DeliveryStatsDto>;

[Logging(Order = 1)]
public class GetDeliveryStatsHandler : IRequestHandler<GetDeliveryStats, DeliveryStatsDto>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public GetDeliveryStatsHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<DeliveryStatsDto>> HandleAsync(GetDeliveryStats request, CancellationToken cancellationToken = default)
    {
        return _webhookService.GetDeliveryStatsAsync(_scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
