using Asp.Versioning;
using Identity.Contracts.Constants;
using Identity.Contracts.Dtos;
using Identity.Features.ApiKeys;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>
/// API key management endpoints for machine-to-machine, CLI, and integration authentication.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/api-keys")]
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

    #region CreateApiKey

    /// <summary>
    /// Request model for creating a new API key.
    /// </summary>
    public record CreateApiKeyRequest(
        string Name,
        DateTime? ExpiresAt = null,
        DefaultId? UserId = null);

    /// <summary>
    /// Generate a new API key. Returns the plaintext API key secret once upon creation.
    /// </summary>
    [HttpPost]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreateApiKeyRequest body,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var isElevated = _scopeContext.IsSystem;
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

    #endregion

    #region GetApiKeys

    /// <summary>
    /// Get a paginated list of API keys. Regular users can only see their own keys;
    /// administrators can view keys for all users or filter by user ID.
    /// </summary>
    [HttpGet]
    [AuthorizePolicy<AuthenticatedPolicy>]
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

        var isElevated = _scopeContext.IsSystem;

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

    #endregion

    #region GetApiKey

    /// <summary>
    /// Get API key metadata by ID (without the secret).
    /// </summary>
    [HttpGet("{id}")]
    [AuthorizePolicy<AuthenticatedPolicy>]
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
                var isElevated = _scopeContext.IsSystem;

                if (key.UserId != _scopeContext.UserId.Value && !isElevated)
                {
                    return Forbid();
                }

                return Ok(key);
            },
            failure => NotFound(new { message = failure.Message }));
    }

    #endregion

    #region RevokeApiKey

    /// <summary>
    /// Revoke an API key.
    /// </summary>
    [HttpDelete("{id}")]
    [HttpPost("{id}/revoke")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> RevokeApiKey(
        [FromRoute] DefaultId id,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var isElevated = _scopeContext.IsSystem;

        var result = await _dispatcher.DispatchAsync(
            new RevokeApiKey(id, _scopeContext.UserId.Value, isElevated), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion
}
