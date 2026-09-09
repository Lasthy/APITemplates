namespace Webhooks.Contracts.Constants;

/// <summary>
/// HTTP header names used in webhook deliveries.
/// </summary>
public static class WebhookHeaders
{
    public const string WebhookId = "X-Webhook-Id";
    public const string WebhookEvent = "X-Webhook-Event";
    public const string WebhookSignature = "X-Webhook-Signature";
    public const string WebhookTimestamp = "X-Webhook-Timestamp";
}
