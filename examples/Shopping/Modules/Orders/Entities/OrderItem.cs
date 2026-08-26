namespace Orders.Entities;

/// <summary>
/// Line item associated with a finalized order.
/// </summary>
public class OrderItem
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public DefaultId OrderId { get; set; }
    public DefaultId ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal TotalPrice => UnitPrice * Quantity;

    public Order? Order { get; set; }
}
