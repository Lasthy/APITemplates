using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Contracts.Events;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Orders;

public record MarkOrderAsDelivered(DefaultId OrderId) : IRequest<OrderDto>;

public class MarkOrderAsDeliveredHandler : IRequestHandler<MarkOrderAsDelivered, OrderDto>
{
    private readonly OrdersDbContext _db;
    private readonly IEventBus _eventBus;

    public MarkOrderAsDeliveredHandler(OrdersDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public async Task<Result<OrderDto>> HandleAsync(MarkOrderAsDelivered request, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order == null)
        {
            return Result.Failure<OrderDto>(new KeyNotFoundException($"Order with ID '{request.OrderId}' was not found."));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return Result.Failure<OrderDto>(new InvalidOperationException("Cannot mark a cancelled order as delivered."));
        }

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new OrderDeliveredEvent(order.Id, order.DeliveredAt.Value), ct);

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
