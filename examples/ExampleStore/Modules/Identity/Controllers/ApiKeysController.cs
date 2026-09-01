using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Identity.Features.ApiKeys;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>
/// API key management endpoints for machine-to-machine, CLI, and integration authentication.
/// </summary>
[ApiController]
[Route("api/v1/identity/api-keys")]
[ApiExplorerSettings(GroupName = "Identity")]
[Tags("ApiKeys")]
public class ApiKeysController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IScopeContext _scopeContext;

    public ApiKeysController(IDispatcher dispatcher, IScopeContext scopeContext)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
    }

    /// <summary>
    /// Generate a new API key. Returns the plaintext API key secret once upon creation.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreateApiKeyRequest body,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var isElevated = _scopeContext.IsSystem ||
                         _scopeContext.IsInRole(IdentityRoles.Admin) ||
                         _scopeContext.IsInRole("Manager");

        var targetUserId = body.UserId ?? _scopeContext.UserId.Value;

        if (targetUserId != _scopeContext.UserId.Value && !isElevated)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateApiKey(targetUserId, body.Name, body.ExpiresAt), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetApiKey), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get a paginated list of API keys. Regular users can only see their own keys;
    /// administrators can view keys for all users or filter by user ID.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetApiKeys(
        [FromQuery] DefaultId? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var isElevated = _scopeContext.IsSystem ||
                         _scopeContext.IsInRole(IdentityRoles.Admin) ||
                         _scopeContext.IsInRole("Manager");

        if (userId.HasValue && userId.Value != _scopeContext.UserId.Value && !isElevated)
        {
            return Forbid();
        }

        var targetUserId = userId ?? (!isElevated ? _scopeContext.UserId : null);

        var result = await _dispatcher.DispatchAsync(
            new GetApiKeys(targetUserId, page, pageSize), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get API key metadata by ID (without the secret).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetApiKey(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(new GetApiKeyById(id), ct);

        return result.Match<IActionResult>(
            key =>
            {
                var isElevated = _scopeContext.IsSystem ||
                                 _scopeContext.IsInRole(IdentityRoles.Admin) ||
                                 _scopeContext.IsInRole("Manager");

                if (key.UserId != _scopeContext.UserId.Value && !isElevated)
                {
                    return Forbid();
                }

                return Ok(key);
            },
            failure => NotFound(new { message = failure.Message }));
    }

    /// <summary>
    /// Revoke an API key.
    /// </summary>
    [HttpDelete("{id}")]
    [HttpPost("{id}/revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeApiKey(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var isElevated = _scopeContext.IsSystem ||
                         _scopeContext.IsInRole(IdentityRoles.Admin) ||
                         _scopeContext.IsInRole("Manager");

        var result = await _dispatcher.DispatchAsync(
            new RevokeApiKey(id, _scopeContext.UserId.Value, isElevated), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }
}

/// <summary>
/// Request model for creating a new API key.
/// </summary>
public record CreateApiKeyRequest(
    string Name,
    DateTime? ExpiresAt = null,
    DefaultId? UserId = null);
