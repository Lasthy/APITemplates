using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Orders;

public record GetOrders(int Page = 1, int PageSize = 20, string? Status = null) : IRequest<IReadOnlyList<OrderDto>>;

public class GetOrdersHandler : IRequestHandler<GetOrders, IReadOnlyList<OrderDto>>
{
    private readonly OrdersDbContext _db;

    public GetOrdersHandler(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> HandleAsync(GetOrders request, CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(o => o.Status == status);
        }

        var orders = await query
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
