namespace Webhooks.Contracts.Constants;

/// <summary>
/// Authorization policy names for the Webhooks module.
/// </summary>
public static class WebhookPolicies
{
    /// <summary>
    /// Required policy to manage webhook subscriptions and deliveries.
    /// </summary>
    public const string ManageWebhooks = "Webhooks.ManageWebhooks";
}
