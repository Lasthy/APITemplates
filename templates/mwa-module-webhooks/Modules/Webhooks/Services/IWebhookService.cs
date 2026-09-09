using Kernel.Common;
using Webhooks.Contracts.Dtos;
using Webhooks.Entities;

namespace Webhooks.Services;

public record CreateSubscriptionRequest(
    string Name,
    string Url,
    string? EventTypes = null,
    string? Description = null,
    string? CustomHeadersJson = null);

public record UpdateSubscriptionRequest(
    string? Name = null,
    string? Url = null,
    string? EventTypes = null,
    string? Description = null,
    string? CustomHeadersJson = null);

public record DeliveriesFilter(
    DefaultId? SubscriptionId = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// Business service operations for managing webhook subscriptions and deliveries.
/// </summary>
public interface IWebhookService
{
    // Subscription operations
    Task<Result<WebhookSubscriptionDetailDto>> CreateSubscriptionAsync(CreateSubscriptionRequest request, DefaultId ownerUserId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<WebhookSubscriptionDto>>> GetSubscriptionsAsync(DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<WebhookSubscriptionDetailDto>> GetSubscriptionAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<WebhookSubscriptionDetailDto>> UpdateSubscriptionAsync(DefaultId subscriptionId, UpdateSubscriptionRequest request, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result> DeleteSubscriptionAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result> PauseSubscriptionAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result> ResumeSubscriptionAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<string>> RotateSecretAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<WebhookDeliveryDetailDto>> TestSubscriptionAsync(DefaultId subscriptionId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);

    // Delivery operations
    Task<Result<IReadOnlyList<WebhookDeliveryDto>>> GetDeliveriesAsync(DeliveriesFilter filter, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<WebhookDeliveryDetailDto>> GetDeliveryAsync(DefaultId deliveryId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<WebhookDeliveryDetailDto>> ReplayDeliveryAsync(DefaultId deliveryId, DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
    Task<Result<DeliveryStatsDto>> GetDeliveryStatsAsync(DefaultId? currentUserId, bool isSystem, CancellationToken ct = default);
}
