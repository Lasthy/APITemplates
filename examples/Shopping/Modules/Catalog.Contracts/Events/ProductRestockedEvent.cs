using Kernel.Events;

namespace Catalog.Contracts.Events;

/// <summary>
/// Domain event published when a product is restocked.
/// </summary>
public record ProductRestockedEvent(
    DefaultId ProductId,
    int AddedQuantity,
    int NewStockQuantity) : Event;
