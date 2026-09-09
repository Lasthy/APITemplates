namespace Webhooks.Contracts.Dtos;

/// <summary>
/// Summary representation of a webhook subscription.
/// </summary>
public record WebhookSubscriptionDto(
    DefaultId Id,
    string Name,
    string Url,
    string Status,
    string? EventTypes,
    DateTime CreatedAt);
