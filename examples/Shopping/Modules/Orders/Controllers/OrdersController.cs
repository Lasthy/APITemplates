using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orders.Contracts.Dtos;
using Orders.Features.Checkout;
using Orders.Features.Orders;

namespace Orders.Controllers;

[ApiController]
[Route("api/v1/orders")]
[ApiExplorerSettings(GroupName = "Orders")]
[Tags("Orders")]
public class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OrdersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Checkout the active user's cart and place a new order (Client or System).
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> Checkout(CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new Checkout(), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetOrder), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// List orders placed by the current user (Client or System).
    /// </summary>
    [HttpGet("my-orders")]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetMyOrders(page, pageSize), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get order details by ID (Owner, Employee, Manager, or System).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetOrder([FromRoute] DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetOrderById(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    /// <summary>
    /// List all store orders with optional status filter (Employee, Manager, System).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetOrders(page, pageSize, status), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Confirm purchase fulfillment: mark order as sent (Employee, Manager, System).
    /// </summary>
    [HttpPost("{id}/send")]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> MarkAsSent([FromRoute] DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new MarkOrderAsSent(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Confirm purchase delivery: mark order as delivered (Employee, Manager, System).
    /// </summary>
    [HttpPost("{id}/deliver")]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> MarkAsDelivered([FromRoute] DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new MarkOrderAsDelivered(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Cancel an order and restore product stock (Manager or System only).
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> CancelOrder(
        [FromRoute] DefaultId id,
        [FromBody] CancelOrderRequest body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new CancelOrder(id, body.Reason), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }
}

public record CancelOrderRequest(string Reason);
