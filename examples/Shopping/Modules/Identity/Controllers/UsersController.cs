using Identity.Contracts.Constants;
using Identity.Features.Users;
using Identity.Profiles;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>
/// User management endpoints: register, list, get, update, ban/unban, preferences, dummy users.
/// </summary>
[ApiController]
[Route("api/v1/identity/users")]
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

    /// <summary>
    /// Register a new client user.
    /// Anonymous users can register as Client.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
    {
        var profile = string.IsNullOrWhiteSpace(body.ProfileName) ? IdentityProfiles.Client : body.ProfileName;

        // If not authenticated or not manager/system, only allow public profiles
        var isManagerOrSystem = _scopeContext.IsAuthenticated &&
            (_scopeContext.IsInRole(IdentityRoles.Manager) || _scopeContext.IsInRole(IdentityRoles.System));

        if (!isManagerOrSystem && !_profileRegistry.IsPublic(profile))
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateUser(body.Email, body.UserName, body.Password, profile), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetUser), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Create a new employee user (Manager or System only).
    /// </summary>
    [HttpPost("employees")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateUser(body.Email, body.UserName, body.Password, IdentityProfiles.Employee), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetUser), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Create a new manager user (System super-user only).
    /// </summary>
    [HttpPost("managers")]
    [Authorize(Roles = IdentityRoles.System)]
    public async Task<IActionResult> CreateManager([FromBody] CreateManagerRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new CreateUser(body.Email, body.UserName, body.Password, IdentityProfiles.Manager), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetUser), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get a paginated list of all users (Manager or System only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
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

    /// <summary>
    /// Get a user's full details by ID (Manager, System, or self).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetUser([FromRoute] DefaultId id, CancellationToken ct)
    {
        var isManagerOrSystem = _scopeContext.IsInRole(IdentityRoles.Manager) || _scopeContext.IsInRole(IdentityRoles.System);
        var isSelf = _scopeContext.UserId == id;
        if (!isManagerOrSystem && !isSelf)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(new Features.Users.GetUser(id), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    /// <summary>
    /// Update a user's email and/or username (Manager, System, or self).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] DefaultId id,
        [FromBody] UpdateUserRequest body,
        CancellationToken ct)
    {
        var isManagerOrSystem = _scopeContext.IsInRole(IdentityRoles.Manager) || _scopeContext.IsInRole(IdentityRoles.System);
        var isSelf = _scopeContext.UserId == id;
        if (!isManagerOrSystem && !isSelf)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new UpdateUser(id, body.Email, body.UserName), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Update user preferences (self only).
    /// </summary>
    [HttpPut("{id}/preferences")]
    [Authorize]
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

    /// <summary>
    /// Ban a user (Manager or System only). Revokes all active refresh tokens.
    /// </summary>
    [HttpPost("{id}/ban")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
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

    /// <summary>
    /// Unban a user (Manager or System only).
    /// </summary>
    [HttpPost("{id}/unban")]
    [Authorize(Roles = $"{IdentityRoles.Manager},{IdentityRoles.System}")]
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

    /// <summary>
    /// Create a dummy user cloning the roles/claims of the specified user (System role only).
    /// Returns a JWT for the dummy user.
    /// </summary>
    [HttpPost("{id}/dummy")]
    [Authorize(Roles = IdentityRoles.System)]
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
}

// ── Request body models ───────────────────────────────────────────────────────

/// <summary>User registration request body.</summary>
public record RegisterRequest(string Email, string UserName, string Password, string? ProfileName = null);

/// <summary>Employee creation request body.</summary>
public record CreateEmployeeRequest(string Email, string UserName, string Password);

/// <summary>Manager creation request body.</summary>
public record CreateManagerRequest(string Email, string UserName, string Password);

/// <summary>User update request body.</summary>
public record UpdateUserRequest(string? Email, string? UserName);

/// <summary>User preferences update request body.</summary>
public record UpdatePreferencesRequest(string PreferencesJson);

/// <summary>Ban request body.</summary>
public record BanRequest(string? Reason);
