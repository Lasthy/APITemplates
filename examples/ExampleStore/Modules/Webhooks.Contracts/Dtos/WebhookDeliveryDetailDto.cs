namespace Webhooks.Contracts.Dtos;

/// <summary>
/// Detailed representation of a webhook delivery attempt including payloads and responses.
/// </summary>
public record WebhookDeliveryDetailDto(
    DefaultId Id,
    DefaultId SubscriptionId,
    string EventType,
    string Status,
    int? ResponseStatusCode,
    string? ResponseBody,
    string RequestBody,
    string? Error,
    int AttemptNumber,
    long? DurationMs,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? NextRetryAt);
