using Identity.Contracts.Constants;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orders.Contracts.Dtos;
using Orders.Features.Reports;

namespace Orders.Controllers;

[ApiController]
[Route("api/v1/orders/reports")]
[ApiExplorerSettings(GroupName = "Orders")]
[Tags("Reports")]
public class ReportsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ReportsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Generate a report of all products sold within a specified time period (Manager, System).
    /// </summary>
    [HttpGet("products-sold")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> GetProductsSoldReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetProductsSoldReport(from, to), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Generate a revenue report within a specified time period (Manager, System).
    /// GroupBy options: 'day', 'month', 'total' (default: 'day').
    /// </summary>
    [HttpGet("revenue")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetRevenueReport(from, to, groupBy), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }
}
