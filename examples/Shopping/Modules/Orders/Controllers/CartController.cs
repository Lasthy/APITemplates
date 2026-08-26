using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orders.Contracts.Dtos;
using Orders.Features.Cart;

namespace Orders.Controllers;

[ApiController]
[Route("api/v1/orders/cart")]
[ApiExplorerSettings(GroupName = "Orders")]
[Tags("Cart")]
public class CartController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CartController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// View the active user's shopping cart.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> GetCart(CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetCart(), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Add an item to the shopping cart.
    /// </summary>
    [HttpPost("items")]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new AddToCart(body.ProductId, body.Quantity), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Update the quantity of an item in the shopping cart.
    /// </summary>
    [HttpPut("items/{productId}")]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> UpdateQuantity(
        [FromRoute] DefaultId productId,
        [FromBody] UpdateCartItemRequest body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UpdateCartItemQuantity(productId, body.Quantity), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Remove an item from the shopping cart.
    /// </summary>
    [HttpDelete("items/{productId}")]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> RemoveItem([FromRoute] DefaultId productId, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new RemoveCartItem(productId), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Clear all items from the shopping cart.
    /// </summary>
    [HttpDelete]
    [Authorize(Roles = $"{IdentityRoles.Client},{IdentityRoles.System}")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new ClearCart(), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }
}

public record AddCartItemRequest(DefaultId ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);
