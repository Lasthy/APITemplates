using Kernel.Requests;

namespace Catalog.Contracts.Requests;

/// <summary>
/// Cross-module command to deduct inventory stock for a set of items during checkout.
/// </summary>
public record DeductProductStock(IReadOnlyList<StockDeductionItem> Items) : IRequest<bool>;

/// <summary>
/// Individual product stock deduction specification.
/// </summary>
public record StockDeductionItem(DefaultId ProductId, int Quantity);
