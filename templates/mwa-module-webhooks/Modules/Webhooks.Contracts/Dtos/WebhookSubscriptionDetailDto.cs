namespace Webhooks.Contracts.Dtos;

/// <summary>
/// Detailed representation of a webhook subscription including performance statistics and optional secret.
/// </summary>
public record WebhookSubscriptionDetailDto(
    DefaultId Id,
    string Name,
    string Url,
    string Status,
    string? EventTypes,
    string? Description,
    long TotalDeliveries,
    long TotalFailures,
    int ConsecutiveFailures,
    DateTime? LastDeliveryAt,
    DateTime CreatedAt,
    string? Secret = null);
