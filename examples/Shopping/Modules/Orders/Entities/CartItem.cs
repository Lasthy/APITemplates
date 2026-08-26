using Kernel.Entities;

namespace Orders.Entities;

/// <summary>
/// Represents an item in a user's shopping cart.
/// </summary>
public class CartItem : IAuditable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public DefaultId UserId { get; set; }
    public DefaultId ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal TotalPrice => UnitPrice * Quantity;

    // IAuditable implementation
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
