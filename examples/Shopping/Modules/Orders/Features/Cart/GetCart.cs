using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Cart;

public record GetCart : IRequest<CartDto>;

public class GetCartHandler : IRequestHandler<GetCart, CartDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;

    public GetCartHandler(OrdersDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<CartDto>> HandleAsync(GetCart request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<CartDto>(new UnauthorizedAccessException("User is not authenticated."));
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

        var cartDto = new CartDto(userId.Value, items, totalItems, totalPrice);
        return Result.Success(cartDto);
    }
}
