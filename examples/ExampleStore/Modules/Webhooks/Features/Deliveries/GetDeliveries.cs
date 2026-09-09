using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Contracts.Dtos;
using Webhooks.Services;

namespace Webhooks.Features.Deliveries;

/// <summary>
/// Query to retrieve a filtered list of webhook deliveries.
/// </summary>
public record GetDeliveries(
    DefaultId? SubscriptionId = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 50) : IRequest<IReadOnlyList<WebhookDeliveryDto>>;

[Logging(Order = 1)]
public class GetDeliveriesHandler : IRequestHandler<GetDeliveries, IReadOnlyList<WebhookDeliveryDto>>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public GetDeliveriesHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<IReadOnlyList<WebhookDeliveryDto>>> HandleAsync(GetDeliveries request, CancellationToken cancellationToken = default)
    {
        var filter = new DeliveriesFilter(
            request.SubscriptionId,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize);

        return _webhookService.GetDeliveriesAsync(filter, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
