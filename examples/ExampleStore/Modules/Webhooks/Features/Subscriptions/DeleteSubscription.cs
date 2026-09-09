using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to soft-delete a webhook subscription.
/// </summary>
public record DeleteSubscription(DefaultId Id) : IRequest<Result>;

[Logging(Order = 1)]
public class DeleteSubscriptionHandler : IRequestHandler<DeleteSubscription, Result>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public DeleteSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public async Task<Result<Result>> HandleAsync(DeleteSubscription request, CancellationToken cancellationToken = default)
    {
        var result = await _webhookService.DeleteSubscriptionAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
        return result.IsSuccess ? Result.Success(Result.Success()) : Result.Failure<Result>(result.Error!);
    }
}
