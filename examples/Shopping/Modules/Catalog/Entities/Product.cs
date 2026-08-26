using Kernel.Entities;

namespace Catalog.Entities;

/// <summary>
/// Represents a product in the catalog with stock and soft-delete support.
/// </summary>
public class Product : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;

    public bool IsLowStock => StockQuantity <= LowStockThreshold;

    // IAuditable implementation
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ISoftDeletable implementation
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
