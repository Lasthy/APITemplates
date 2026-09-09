using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Published when a webhook subscription is paused or automatically disabled due to failures.
/// </summary>
public record WebhookSubscriptionPausedEvent(DefaultId SubscriptionId, string Reason) : Event;
