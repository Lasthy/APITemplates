namespace Orders.Contracts.Dtos;

/// <summary>
/// Representation of an item within a finalized order.
/// </summary>
public record OrderItemDto(
    DefaultId Id,
    DefaultId ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);

/// <summary>
/// Representation of a customer order.
/// </summary>
public record OrderDto(
    DefaultId Id,
    string OrderNumber,
    DefaultId UserId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? SentAt = null,
    DateTime? DeliveredAt = null,
    DateTime? CancelledAt = null,
    string? CancellationReason = null,
    IReadOnlyList<OrderItemDto>? Items = null);
