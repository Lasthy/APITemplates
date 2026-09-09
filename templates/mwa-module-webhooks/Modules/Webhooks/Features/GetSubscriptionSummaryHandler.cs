using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Webhooks.Contracts.Dtos;
using Webhooks.Contracts.Requests;
using Webhooks.Persistence;

namespace Webhooks.Features;

/// <summary>
/// Cross-module handler for <see cref="GetSubscriptionSummary"/>.
/// Allows other modules to query webhook subscription summary information by subscription ID.
/// </summary>
[Logging(Order = 1)]
public class GetSubscriptionSummaryHandler : IRequestHandler<GetSubscriptionSummary, WebhookSubscriptionDto>
{
    private readonly WebhookModuleDbContext _dbContext;

    public GetSubscriptionSummaryHandler(WebhookModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<WebhookSubscriptionDto>> HandleAsync(GetSubscriptionSummary request, CancellationToken cancellationToken = default)
    {
        var subscription = await _dbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId && s.DeletedAt == null, cancellationToken);

        if (subscription == null)
        {
            return Result.Failure<WebhookSubscriptionDto>(
                new KeyNotFoundException($"Webhook subscription '{request.SubscriptionId}' not found."));
        }

        return Result.Success(new WebhookSubscriptionDto(
            subscription.Id,
            subscription.Name,
            subscription.Url,
            subscription.Status,
            subscription.EventTypes,
            subscription.CreatedAt));
    }
}
