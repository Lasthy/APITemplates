namespace Orders.Contracts.Dtos;

/// <summary>
/// Representation of an item in the user's shopping cart.
/// </summary>
public record CartItemDto(
    DefaultId Id,
    DefaultId ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);

/// <summary>
/// Representation of the user's active shopping cart with aggregated totals.
/// </summary>
public record CartDto(
    DefaultId UserId,
    IReadOnlyList<CartItemDto> Items,
    int TotalItems,
    decimal TotalPrice);
