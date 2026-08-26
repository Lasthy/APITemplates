using Kernel.Events;

namespace Catalog.Contracts.Events;

/// <summary>
/// Domain event published when a new product is added to the catalog.
/// </summary>
public record ProductCreatedEvent(
    DefaultId ProductId,
    string Name,
    string Sku,
    decimal Price,
    int StockQuantity) : Event;
