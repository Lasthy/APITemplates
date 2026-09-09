using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Published when a webhook delivery succeeds.
/// </summary>
public record WebhookDeliverySucceededEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType) : Event;
