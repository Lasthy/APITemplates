using Catalog.Contracts.Requests;
using Kernel.Common;
using Kernel.Context;
using Kernel.Events;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Contracts.Events;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Checkout;

public record Checkout : IRequest<OrderDto>;

public class CheckoutHandler : IRequestHandler<Checkout, OrderDto>
{
    private readonly OrdersDbContext _db;
    private readonly IScopeContext _scopeContext;
    private readonly IDispatcher _dispatcher;
    private readonly IEventBus _eventBus;

    public CheckoutHandler(
        OrdersDbContext db,
        IScopeContext scopeContext,
        IDispatcher dispatcher,
        IEventBus eventBus)
    {
        _db = db;
        _scopeContext = scopeContext;
        _dispatcher = dispatcher;
        _eventBus = eventBus;
    }

    public async Task<Result<OrderDto>> HandleAsync(Checkout request, CancellationToken ct = default)
    {
        var userId = _scopeContext.UserId;
        if (userId == null)
        {
            return Result.Failure<OrderDto>(new UnauthorizedAccessException("User is not authenticated."));
        }

        var cartItems = await _db.CartItems
            .Where(c => c.UserId == userId.Value)
            .ToListAsync(ct);

        if (cartItems.Count == 0)
        {
            return Result.Failure<OrderDto>(new InvalidOperationException("Shopping cart is empty."));
        }

        // Prepare stock deduction items for Catalog cross-module dispatch
        var deductionItems = cartItems
            .Select(c => new StockDeductionItem(c.ProductId, c.Quantity))
            .ToList();

        var deductionResult = await _dispatcher.DispatchAsync(new DeductProductStock(deductionItems), ct);
        if (deductionResult.IsFailure)
        {
            return Result.Failure<OrderDto>(deductionResult.Error ?? new InvalidOperationException("Failed to deduct inventory stock."));
        }

        var customerEmail = _scopeContext.GetClaim("email")
            ?? _scopeContext.GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
            ?? "customer@shopping.local";

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var totalAmount = cartItems.Sum(c => c.TotalPrice);

        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = userId.Value,
            CustomerEmail = customerEmail,
            Status = OrderStatus.Paid,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var cartItem in cartItems)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                ProductName = cartItem.ProductName,
                UnitPrice = cartItem.UnitPrice,
                Quantity = cartItem.Quantity
            };
            order.Items.Add(orderItem);
        }

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cartItems);

        await _db.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new OrderPlacedEvent(order.Id, order.UserId, order.TotalAmount), ct);

        var orderDto = new OrderDto(
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

        return Result.Success(orderDto);
    }
}
