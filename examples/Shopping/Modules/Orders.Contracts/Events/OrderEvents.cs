using Kernel.Events;

namespace Orders.Contracts.Events;

/// <summary>
/// Domain event published when an order is created / placed by a client.
/// </summary>
public record OrderPlacedEvent(
    DefaultId OrderId,
    DefaultId UserId,
    decimal TotalAmount) : Event;

/// <summary>
/// Domain event published when an order is marked as sent.
/// </summary>
public record OrderSentEvent(
    DefaultId OrderId,
    DateTime SentAt) : Event;

/// <summary>
/// Domain event published when an order is marked as delivered.
/// </summary>
public record OrderDeliveredEvent(
    DefaultId OrderId,
    DateTime DeliveredAt) : Event;

/// <summary>
/// Domain event published when an order is cancelled.
/// </summary>
public record OrderCancelledEvent(
    DefaultId OrderId,
    string Reason,
    DateTime CancelledAt) : Event;
