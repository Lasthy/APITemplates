using Kernel.Requests;
using Webhooks.Contracts.Dtos;

namespace Webhooks.Contracts.Requests;

/// <summary>
/// Cross-module request to retrieve summary information for a webhook subscription.
/// </summary>
public record GetSubscriptionSummary(DefaultId SubscriptionId) : IRequest<WebhookSubscriptionDto>;
