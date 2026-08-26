using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Persistence;

namespace Orders.Features.Cart;

public record ClearCart : IRequest<bool>;

public class ClearCartHandler : IRequestHandler<ClearCart, bool>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;

    public ClearCartHandler(OrdersDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<bool>> HandleAsync(ClearCart request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<bool>(new UnauthorizedAccessException("User is not authenticated."));
        }

        var items = await _db.CartItems
            .Where(c => c.UserId == userId.Value)
            .ToListAsync(ct);

        if (items.Count > 0)
        {
            _db.CartItems.RemoveRange(items);
            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(true);
    }
}
