using Webhooks.Entities;

namespace Webhooks.Delivery;

/// <summary>
/// Abstraction for delivering a webhook event payload to a target subscription.
/// </summary>
public interface IWebhookDeliveryEngine
{
    /// <summary>
    /// Executes an HTTP POST delivery to the specified subscription URL.
    /// </summary>
    /// <param name="subscription">The destination subscription.</param>
    /// <param name="webhookEvent">The event being delivered.</param>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Populated <see cref="WebhookDelivery"/> recording status, timing, and response.</returns>
    Task<WebhookDelivery> DeliverAsync(WebhookSubscription subscription, WebhookEvent webhookEvent, int attemptNumber, CancellationToken ct = default);
}
