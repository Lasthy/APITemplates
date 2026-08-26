using Kernel.Entities;

namespace Orders.Entities;

/// <summary>
/// Represents a customer purchase order.
/// </summary>
public class Order : IAuditable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public string OrderNumber { get; set; } = string.Empty;
    public DefaultId UserId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    // IAuditable implementation
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
