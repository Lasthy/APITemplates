using Catalog.Contracts.Requests;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Cart;

public record UpdateCartItemQuantity(DefaultId ProductId, int Quantity) : IRequest<CartDto>;

public class UpdateCartItemQuantityHandler : IRequestHandler<UpdateCartItemQuantity, CartDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;
    private readonly IDispatcher _dispatcher;

    public UpdateCartItemQuantityHandler(
        OrdersDbContext db,
        IScopeContext scopeContext,
        IDispatcher dispatcher)
    {
        _db = db;
        _scopeContext = scopeContext;
        _dispatcher = dispatcher;
    }

    public async Task<Result<CartDto>> HandleAsync(UpdateCartItemQuantity request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<CartDto>(new UnauthorizedAccessException("User is not authenticated."));
        }

        var item = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == request.ProductId, ct);

        if (item == null)
        {
            return Result.Failure<CartDto>(new KeyNotFoundException("Item not found in cart."));
        }

        if (request.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            // Verify stock
            var productResult = await _dispatcher.DispatchAsync(new GetProductSummary(request.ProductId), ct);
            if (productResult.IsSuccess && productResult.Value != null && request.Quantity > productResult.Value.StockQuantity)
            {
                return Result.Failure<CartDto>(new InvalidOperationException(
                    $"Requested quantity ({request.Quantity}) exceeds available stock ({productResult.Value.StockQuantity})."));
            }

            item.Quantity = request.Quantity;
        }

        await _db.SaveChangesAsync(ct);

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
