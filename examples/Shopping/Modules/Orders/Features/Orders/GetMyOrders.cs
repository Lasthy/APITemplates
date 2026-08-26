using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Orders;

public record GetMyOrders(int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<OrderDto>>;

public class GetMyOrdersHandler : IRequestHandler<GetMyOrders, IReadOnlyList<OrderDto>>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;

    public GetMyOrdersHandler(OrdersDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> HandleAsync(GetMyOrders request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<IReadOnlyList<OrderDto>>(new UnauthorizedAccessException("User is not authenticated."));
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id,
                o.OrderNumber,
                o.UserId,
                o.CustomerEmail,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.SentAt,
                o.DeliveredAt,
                o.CancelledAt,
                o.CancellationReason,
                o.Items.Select(i => new OrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.ProductName,
                    i.UnitPrice,
                    i.Quantity,
                    i.TotalPrice)).ToList()))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<OrderDto>>(orders);
    }
}
