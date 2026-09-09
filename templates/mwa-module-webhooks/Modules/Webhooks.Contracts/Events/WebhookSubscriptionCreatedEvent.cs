using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Published when a new webhook subscription is created.
/// </summary>
public record WebhookSubscriptionCreatedEvent(DefaultId SubscriptionId, string Url, string? EventTypes) : Event;
