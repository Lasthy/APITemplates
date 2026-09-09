using Kernel.Events;

namespace Webhooks.Contracts.Events;

/// <summary>
/// Domain event dispatched to test connectivity of a webhook subscription.
/// </summary>
public record WebhookTestEvent(DefaultId SubscriptionId, string Message, DateTime SentAt) : Event;
