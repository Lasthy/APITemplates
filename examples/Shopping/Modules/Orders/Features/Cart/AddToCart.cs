using Catalog.Contracts.Requests;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Cart;

public record AddToCart(DefaultId ProductId, int Quantity) : IRequest<CartDto>;

public class AddToCartHandler : IRequestHandler<AddToCart, CartDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;
    private readonly IDispatcher _dispatcher;

    public AddToCartHandler(
        OrdersDbContext db,
        IScopeContext scopeContext,
        IDispatcher dispatcher)
    {
        _db = db;
        _scopeContext = scopeContext;
        _dispatcher = dispatcher;
    }

    public async Task<Result<CartDto>> HandleAsync(AddToCart request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<CartDto>(new UnauthorizedAccessException("User is not authenticated."));
        }

        if (request.Quantity <= 0)
        {
            return Result.Failure<CartDto>(new ArgumentException("Quantity must be greater than zero."));
        }

        // Cross-module dispatch to Catalog to get product info & current stock
        var productResult = await _dispatcher.DispatchAsync(new GetProductSummary(request.ProductId), ct);
        if (productResult.IsFailure || productResult.Value == null)
        {
            return Result.Failure<CartDto>(productResult.Error ?? new KeyNotFoundException("Product not found."));
        }

        var product = productResult.Value;

        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == request.ProductId, ct);

        var newTotalQuantity = (existingItem?.Quantity ?? 0) + request.Quantity;
        if (newTotalQuantity > product.StockQuantity)
        {
            return Result.Failure<CartDto>(new InvalidOperationException(
                $"Cannot add {request.Quantity} item(s). Requested total in cart ({newTotalQuantity}) exceeds available stock ({product.StockQuantity})."));
        }

        if (existingItem != null)
        {
            existingItem.Quantity = newTotalQuantity;
            existingItem.UnitPrice = product.Price; // Update to latest price
            existingItem.ProductName = product.Name;
        }
        else
        {
            var cartItem = new CartItem
            {
                UserId = userId.Value,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = request.Quantity
            };
            _db.CartItems.Add(cartItem);
        }

        await _db.SaveChangesAsync(ct);

        // Return updated cart
        var items = await _db.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId.Value)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CartItemDto(
                c.Id,
                c.ProductId,
                c.ProductName,
                c.UnitPrice,
                c.Quantity,
                c.TotalPrice))
            .ToListAsync(ct);

        var totalItems = items.Sum(i => i.Quantity);
        var totalPrice = items.Sum(i => i.TotalPrice);

        return Result.Success(new CartDto(userId.Value, items, totalItems, totalPrice));
    }
}
