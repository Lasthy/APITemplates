using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Published when a webhook delivery attempt fails.
/// </summary>
public record WebhookDeliveryFailedEvent(DefaultId SubscriptionId, DefaultId DeliveryId, string EventType, int? StatusCode, string Error) : Event;
