using Kernel.Requests;

namespace Catalog.Contracts.Requests;

/// <summary>
/// Cross-module command to restore inventory stock when an order is cancelled.
/// </summary>
public record RestoreProductStock(IReadOnlyList<StockRestorationItem> Items) : IRequest<bool>;

/// <summary>
/// Individual product stock restoration specification.
/// </summary>
public record StockRestorationItem(DefaultId ProductId, int Quantity);
