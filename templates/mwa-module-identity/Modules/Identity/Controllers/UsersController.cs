using Asp.Versioning;
using Identity.Contracts.Constants;
using Identity.Features.Users;
using Identity.Profiles;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace Identity.Controllers;

/// <summary>
/// User management endpoints: register, list, get, update, ban/unban, preferences, dummy users.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/users")]
[ApiExplorerSettings(GroupName = "Identity")]
[Tags("Users")]
public class UsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IScopeContext _scopeContext;
    private readonly ProfileRegistry _profileRegistry;

    public UsersController(
        IDispatcher dispatcher,
        IScopeContext scopeContext,
        ProfileRegistry profileRegistry)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
        _profileRegistry = profileRegistry;
    }

    #region Register

    /// <summary>
    /// User registration request body.
    /// </summary>
    public record RegisterRequest(string Email, string UserName, string Password, string ProfileName);

    /// <summary>
    /// Register a new user with a specified profile.
    /// Anonymous users can only register with public profiles.
    /// Authenticated admins can register users with any profile.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
    {
        // If not authenticated or not admin, only allow public profiles
        var isAdmin = _scopeContext.IsAuthenticated && _scopeContext.IsInRole(IdentityRoles.Admin);
        if (!isAdmin && !_profileRegistry.IsPublic(body.ProfileName))
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateUser(body.Email, body.UserName, body.Password, body.ProfileName), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetUser), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region GetUsers

    /// <summary>
    /// Get a paginated list of all users (admin only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = IdentityRoles.Admin)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? profile = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(
            new GetUsers(page, pageSize, profile), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region GetUser

    /// <summary>
    /// Get a user's full details by ID (admin or self).
    /// </summary>
    [HttpGet("{id}")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> GetUser([FromRoute] DefaultId id, CancellationToken ct)
    {
        // Only admins or the user themselves can view details
        var isAdmin = _scopeContext.IsInRole(IdentityRoles.Admin);
        var isSelf = _scopeContext.UserId == id;
        if (!isAdmin && !isSelf)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(new Features.Users.GetUser(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    #endregion

    #region UpdateUser

    /// <summary>
    /// User update request body.
    /// </summary>
    public record UpdateUserRequest(string? Email, string? UserName);

    /// <summary>
    /// Update a user's email and/or username (admin or self).
    /// </summary>
    [HttpPut("{id}")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] DefaultId id,
        [FromBody] UpdateUserRequest body,
        CancellationToken ct)
    {
        var isAdmin = _scopeContext.IsInRole(IdentityRoles.Admin);
        var isSelf = _scopeContext.UserId == id;
        if (!isAdmin && !isSelf)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new UpdateUser(id, body.Email, body.UserName), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region UpdatePreferences

    /// <summary>
    /// User preferences update request body.
    /// </summary>
    public record UpdatePreferencesRequest(string PreferencesJson);

    /// <summary>
    /// Update user preferences (self only).
    /// </summary>
    [HttpPut("{id}/preferences")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> UpdatePreferences(
        [FromRoute] DefaultId id,
        [FromBody] UpdatePreferencesRequest body,
        CancellationToken ct)
    {
        if (_scopeContext.UserId != id)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new UpdatePreferences(id, body.PreferencesJson), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region Ban

    /// <summary>
    /// Ban request body.
    /// </summary>
    public record BanRequest(string? Reason);

    /// <summary>
    /// Ban a user (admin only). Revokes all active refresh tokens.
    /// </summary>
    [HttpPost("{id}/ban")]
    [Authorize(Roles = IdentityRoles.Admin)]
    public async Task<IActionResult> Ban(
        [FromRoute] DefaultId id,
        [FromBody] BanRequest body,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new BanUser(id, _scopeContext.UserId.Value, body.Reason), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region Unban

    /// <summary>
    /// Unban a user (admin only).
    /// </summary>
    [HttpPost("{id}/unban")]
    [Authorize(Roles = IdentityRoles.Admin)]
    public async Task<IActionResult> Unban([FromRoute] DefaultId id, CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new UnbanUser(id, _scopeContext.UserId.Value), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region CreateDummy

    /// <summary>
    /// Create a dummy user cloning the roles/claims of the specified user (System role only).
    /// Returns a JWT for the dummy user.
    /// </summary>
    [HttpPost("{id}/dummy")]
    [Authorize(Roles = Roles.System)]
    public async Task<IActionResult> CreateDummy([FromRoute] DefaultId id, CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateDummyUser(id, _scopeContext.UserId.Value), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion
}
