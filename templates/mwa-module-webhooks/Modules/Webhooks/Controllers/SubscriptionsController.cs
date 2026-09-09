using Asp.Versioning;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Webhooks.Contracts.Dtos;
using Webhooks.Features.Subscriptions;

namespace Webhooks.Controllers;

/// <summary>
/// Endpoints for managing webhook subscriptions.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/webhooks/subscriptions")]
[ApiExplorerSettings(GroupName = "Webhooks")]
[Tags("Subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public SubscriptionsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public record CreateSubscriptionBody(
        string Name,
        string Url,
        string? EventTypes = null,
        string? Description = null,
        string? CustomHeadersJson = null);

    public record UpdateSubscriptionBody(
        string? Name = null,
        string? Url = null,
        string? EventTypes = null,
        string? Description = null,
        string? CustomHeadersJson = null);

    /// <summary>
    /// Create a new webhook subscription.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WebhookSubscriptionDetailDto>> Create(
        [FromBody] CreateSubscriptionBody body,
        CancellationToken ct)
    {
        var command = new CreateSubscription(
            body.Name,
            body.Url,
            body.EventTypes,
            body.Description,
            body.CustomHeadersJson);

        var result = await _dispatcher.DispatchAsync(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(result.Error?.Message ?? "Failed to create subscription.");
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// List subscriptions (own subscriptions for regular users, all subscriptions for System).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookSubscriptionDto>>> GetAll(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetSubscriptions(), ct);
        if (result.IsFailure)
        {
            return BadRequest(result.Error?.Message ?? "Failed to list subscriptions.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get detailed subscription information by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WebhookSubscriptionDetailDto>> GetById(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetSubscription(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to get subscription.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update a webhook subscription.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<WebhookSubscriptionDetailDto>> Update(
        [FromRoute] DefaultId id,
        [FromBody] UpdateSubscriptionBody body,
        CancellationToken ct)
    {
        var command = new UpdateSubscription(
            id,
            body.Name,
            body.Url,
            body.EventTypes,
            body.Description,
            body.CustomHeadersJson);

        var result = await _dispatcher.DispatchAsync(command, ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to update subscription.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Soft-delete a webhook subscription.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteSubscription(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to delete subscription.");
        }

        return NoContent();
    }

    /// <summary>
    /// Pause outbound delivery for a webhook subscription.
    /// </summary>
    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new PauseSubscription(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to pause subscription.");
        }

        return Ok(new { message = "Subscription paused." });
    }

    /// <summary>
    /// Resume outbound delivery for a paused or disabled webhook subscription.
    /// </summary>
    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new ResumeSubscription(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to resume subscription.");
        }

        return Ok(new { message = "Subscription resumed." });
    }

    /// <summary>
    /// Rotate the HMAC secret for a webhook subscription.
    /// </summary>
    [HttpPost("{id}/rotate-secret")]
    public async Task<ActionResult<object>> RotateSecret(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new RotateSecret(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to rotate secret.");
        }

        return Ok(new { secret = result.Value });
    }

    /// <summary>
    /// Send a test event to verify subscription connectivity and payload signing.
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<WebhookDeliveryDetailDto>> Test(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new TestSubscription(id), ct);
        if (result.IsFailure)
        {
            if (result.Error is KeyNotFoundException) return NotFound(result.Error.Message);
            if (result.Error is UnauthorizedAccessException) return Forbid();
            return BadRequest(result.Error?.Message ?? "Failed to send test event.");
        }

        return Ok(result.Value);
    }
}
