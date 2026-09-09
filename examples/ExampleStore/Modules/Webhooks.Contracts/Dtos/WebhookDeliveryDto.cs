namespace Webhooks.Contracts.Dtos;

/// <summary>
/// Summary representation of a webhook delivery attempt.
/// </summary>
public record WebhookDeliveryDto(
    DefaultId Id,
    DefaultId SubscriptionId,
    string EventType,
    string Status,
    int? ResponseStatusCode,
    int AttemptNumber,
    long? DurationMs,
    DateTime CreatedAt);
