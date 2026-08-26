using Catalog.Contracts.Dtos;
using Catalog.Features.Products;
using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Controllers;

[ApiController]
[Route("api/v1/catalog/products")]
[ApiExplorerSettings(GroupName = "Catalog")]
[Tags("Products")]
public class ProductsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Browse and search catalog products.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetProducts(page, pageSize, search), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get low stock products (Employee, Manager, System only).
    /// </summary>
    [HttpGet("low-stock")]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> GetLowStockProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetLowStockProducts(page, pageSize), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get product details by ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProduct([FromRoute] DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetProductById(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    /// <summary>
    /// Create a new product (Manager or System only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateProduct(body.Name, body.Description, body.Sku, body.Price, body.StockQuantity, body.LowStockThreshold), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetProduct), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Update an existing product (Manager or System only).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> UpdateProduct(
        [FromRoute] DefaultId id,
        [FromBody] UpdateProductRequest body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new UpdateProduct(id, body.Name, body.Description, body.Sku, body.Price, body.StockQuantity, body.LowStockThreshold), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Soft delete a product (Manager or System only).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> DeleteProduct([FromRoute] DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteProduct(id), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => NotFound(new { message = failure.Message }));
    }
}

public record CreateProductRequest(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int LowStockThreshold = 5);

public record UpdateProductRequest(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int LowStockThreshold = 5);
