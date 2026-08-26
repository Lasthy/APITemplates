namespace Catalog.Contracts.Dtos;

/// <summary>
/// Data transfer object for product information.
/// </summary>
public record ProductDto(
    DefaultId Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime CreatedAt);
