namespace Webhooks.Contracts.Dtos;

/// <summary>
/// Aggregated statistics for webhook deliveries.
/// </summary>
public record DeliveryStatsDto(
    long TotalDeliveries,
    long Succeeded,
    long Failed,
    long DeadLettered,
    long Pending,
    double AvgDurationMs);
