using Asp.Versioning;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Webhooks.Contracts.Dtos;
using Webhooks.Features.Deliveries;

namespace Webhooks.Controllers;

/// <summary>
/// Endpoints for inspecting and replaying webhook delivery attempts.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/webhooks/deliveries")]
[ApiExplorerSettings(GroupName = "Webhooks")]
[Tags("Deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public DeliveriesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// List webhook delivery attempts matching query criteria.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> GetDeliveries(
        [FromQuery] DefaultId? subscriptionId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetDeliveries(subscriptionId, status, fromDate, toDate, page, pageSize);
        var result = await _dispatcher.DispatchAsync(query, ct);

        if (result.IsFailure)
        {
            return BadRequest(result.Error?.Message ?? "Failed to retrieve deliveries.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get full details of a specific webhook delivery attempt.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WebhookDeliveryDetailDto>> GetById(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetDelivery(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to retrieve delivery.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Replay an existing delivery attempt with identical payload.
    /// </summary>
    [HttpPost("{id}/replay")]
    public async Task<ActionResult<WebhookDeliveryDetailDto>> Replay(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new ReplayDelivery(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to replay delivery.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieve aggregate statistics across webhook deliveries.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<DeliveryStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetDeliveryStats(), ct);
        if (result.IsFailure)
        {
            return BadRequest(result.Error?.Message ?? "Failed to retrieve delivery statistics.");
        }

        return Ok(result.Value);
    }
}
