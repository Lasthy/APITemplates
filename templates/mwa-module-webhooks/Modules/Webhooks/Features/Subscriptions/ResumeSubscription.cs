using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to resume deliveries for a paused or auto-disabled webhook subscription.
/// </summary>
public record ResumeSubscription(DefaultId Id) : IRequest<Result>;

[Logging(Order = 1)]
public class ResumeSubscriptionHandler : IRequestHandler<ResumeSubscription, Result>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public ResumeSubscriptionHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public async Task<Result<Result>> HandleAsync(ResumeSubscription request, CancellationToken cancellationToken = default)
    {
        var result = await _webhookService.ResumeSubscriptionAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
        return result.IsSuccess ? Result.Success(Result.Success()) : Result.Failure<Result>(result.Error!);
    }
}
