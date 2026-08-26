using Catalog.Contracts.Requests;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Contracts.Events;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Orders;

public record CancelOrder(DefaultId OrderId, string Reason) : IRequest<OrderDto>;

public class CancelOrderHandler : IRequestHandler<CancelOrder, OrderDto>
{
    private readonly OrdersDbContext _db;
    private readonly IDispatcher _dispatcher;
    private readonly IEventBus _eventBus;

    public CancelOrderHandler(
        OrdersDbContext db,
        IDispatcher dispatcher,
        IEventBus eventBus)
    {
        _db = db;
        _dispatcher = dispatcher;
        _eventBus = eventBus;
    }

    public async Task<Result<OrderDto>> HandleAsync(CancelOrder request, CancellationToken ct = default)
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
            return Result.Failure<OrderDto>(new InvalidOperationException("Order is already cancelled."));
        }

        if (order.Status == OrderStatus.Delivered)
        {
            return Result.Failure<OrderDto>(new InvalidOperationException("Cannot cancel an order that has already been delivered."));
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = string.IsNullOrWhiteSpace(request.Reason) ? "Cancelled by manager" : request.Reason.Trim();

        // Restore inventory stock in Catalog module via cross-module dispatch
        var restoreItems = order.Items
            .Select(i => new StockRestorationItem(i.ProductId, i.Quantity))
            .ToList();

        var restoreResult = await _dispatcher.DispatchAsync(new RestoreProductStock(restoreItems), ct);
        if (restoreResult.IsFailure)
        {
            return Result.Failure<OrderDto>(restoreResult.Error ?? new InvalidOperationException("Failed to restore stock."));
        }

        await _db.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new OrderCancelledEvent(order.Id, order.CancellationReason, order.CancelledAt.Value), ct);

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
