using Catalog.Contracts.Dtos;
using Catalog.Features.Restock;
using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Controllers;

[ApiController]
[Route("api/v1/catalog/restock-requests")]
[ApiExplorerSettings(GroupName = "Catalog")]
[Tags("Restock")]
public class RestockController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public RestockController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// List restock requests with optional status filter (Employee, Manager, System).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> GetRestockRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetRestockRequests(page, pageSize, status), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Submit a product restock request when store stock is low (Employee, Manager, System).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> CreateRestockRequest(
        [FromBody] CreateRestockRequestBody body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateRestockRequest(body.ProductId, body.Quantity, body.Reason), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Fulfill a restock request, increasing product stock in the catalog (Employee, Manager, System).
    /// </summary>
    [HttpPost("{id}/fulfill")]
    [Authorize(Roles = $"{IdentityRoles.Employee},{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> FulfillRestockRequest(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new FulfillRestockRequest(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }
}

public record CreateRestockRequestBody(
    DefaultId ProductId,
    int Quantity,
    string Reason);
