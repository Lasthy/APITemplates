using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Published when a webhook delivery exhausts all retries and is dead-lettered.
/// </summary>
public record WebhookDeliveryDeadLetteredEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType, int AttemptCount) : Event;
