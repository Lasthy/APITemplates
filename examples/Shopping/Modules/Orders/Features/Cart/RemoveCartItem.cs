using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Cart;

public record RemoveCartItem(DefaultId ProductId) : IRequest<CartDto>;

public class RemoveCartItemHandler : IRequestHandler<RemoveCartItem, CartDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;

    public RemoveCartItemHandler(OrdersDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<CartDto>> HandleAsync(RemoveCartItem request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<CartDto>(new UnauthorizedAccessException("User is not authenticated."));
        }

        var item = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == request.ProductId, ct);

        if (item != null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }

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
