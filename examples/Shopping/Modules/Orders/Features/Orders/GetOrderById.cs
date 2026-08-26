using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Persistence;

namespace Orders.Features.Orders;

public record GetOrderById(DefaultId Id) : IRequest<OrderDto>;

public class GetOrderByIdHandler : IRequestHandler<GetOrderById, OrderDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;

    public GetOrderByIdHandler(OrdersDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<OrderDto>> HandleAsync(GetOrderById request, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);

        if (order == null)
        {
            return Result.Failure<OrderDto>(new KeyNotFoundException($"Order with ID '{request.Id}' was not found."));
        }

        var isStaffOrSystem = _scopeContext.IsInRole(IdentityRoles.Employee) ||
                              _scopeContext.IsInRole(IdentityRoles.Manager) ||
                              _scopeContext.IsInRole(IdentityRoles.System);

        var isOwner = _scopeContext.UserId == order.UserId;

        if (!isStaffOrSystem && !isOwner)
        {
            return Result.Failure<OrderDto>(new UnauthorizedAccessException("Access denied."));
        }

        var dto = new OrderDto(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.CustomerEmail,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.SentAt,
            order.DeliveredAt,
            order.CancelledAt,
            order.CancellationReason,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.UnitPrice,
                i.Quantity,
                i.TotalPrice)).ToList());

        return Result.Success(dto);
    }
}
