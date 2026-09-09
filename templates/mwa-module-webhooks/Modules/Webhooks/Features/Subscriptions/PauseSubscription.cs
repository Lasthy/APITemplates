using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to pause deliveries for a webhook subscription.
/// </summary>
public record PauseSubscription(DefaultId Id) : IRequest<Result>;

[Logging(Order = 1)]
public class PauseSubscriptionHandler : IRequestHandler<PauseSubscription, Result>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public PauseSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public async Task<Result<Result>> HandleAsync(PauseSubscription request, CancellationToken cancellationToken = default)
    {
        var result = await _webhookService.PauseSubscriptionAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
        return result.IsSuccess ? Result.Success(Result.Success()) : Result.Failure<Result>(result.Error!);
    }
}
