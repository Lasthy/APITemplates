using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Webhooks.Services;

namespace Webhooks.Features.Subscriptions;

/// <summary>
/// Command to generate and assign a new HMAC secret to a webhook subscription.
/// </summary>
public record RotateSecret(DefaultId Id) : IRequest<string>;

[Logging(Order = 1)]
public class RotateSecretHandler : IRequestHandler<RotateSecret, string>
{
    private readonly IWebhookService _webhookService;
    private readonly IScopeContext _scopeContext;

    public RotateSecretHandler(IWebhookService webhookService, IScopeContext scopeContext)
    {
        _webhookService = webhookService;
        _scopeContext = scopeContext;
    }

    public Task<Result<string>> HandleAsync(RotateSecret request, CancellationToken cancellationToken = default)
    {
        return _webhookService.RotateSecretAsync(request.Id, _scopeContext.UserId, _scopeContext.IsSystem, cancellationToken);
    }
}
