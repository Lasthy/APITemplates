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
    private readonly IProfileRequirementEvaluator _profileRequirementEvaluator;

    public UsersController(
        IDispatcher dispatcher,
        IScopeContext scopeContext,
        ProfileRegistry profileRegistry,
        IProfileRequirementEvaluator profileRequirementEvaluator)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
        _profileRegistry = profileRegistry;
        _profileRequirementEvaluator = profileRequirementEvaluator;
    }

    #region Register

    /// <summary>
    /// User registration request body.
    /// </summary>
    public record RegisterRequest(
        string Email,
        string UserName,
        string Password,
        string ProfileName,
        IReadOnlyDictionary<string, object>? AdditionalData = null);

    /// <summary>
    /// Register a new user with a specified profile.
    /// Creation requirements declared on the profile are evaluated against the caller's context.
    /// System administrators have unrestricted profile creation privileges.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
    {
        var profile = _profileRegistry.Get(body.ProfileName);
        if (profile == null)
        {
            return Forbid();
        }

        var reqContext = new ProfileCreationRequirementContext
        {
            ScopeContext = _scopeContext,
            Profile = profile,
            Email = body.Email,
            UserName = body.UserName,
            Services = HttpContext.RequestServices,
            AdditionalData = body.AdditionalData
        };

        var evalResult = await _profileRequirementEvaluator.EvaluateAsync(reqContext, ct);
        if (evalResult.IsFailure)
        {
            return Forbid();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateUser(body.Email, body.UserName, body.Password, body.ProfileName, body.AdditionalData), ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetUser), new { id = success.Id }, success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region GetUsers

    /// <summary>
    /// Get a paginated list of all users (system only).
    /// </summary>
    [HttpGet]
    [AuthorizePolicy<SystemPolicy>]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? profile = null,
        [FromQuery] bool? isDummy = null,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(
            new GetUsers(page, pageSize, profile, isDummy), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region GetUser

    /// <summary>
    /// Get a user's full details by ID (system or self).
    /// </summary>
    [HttpGet("{id}")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> GetUser([FromRoute] DefaultId id, CancellationToken ct)
    {
        // Only system or the user themselves can view details
        var isElevated = _scopeContext.IsSystem;
        var isSelf = _scopeContext.UserId == id;
        if (!isElevated && !isSelf)
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
    /// Update a user's email and/or username (system or self).
    /// </summary>
    [HttpPut("{id}")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] DefaultId id,
        [FromBody] UpdateUserRequest body,
        CancellationToken ct)
    {
        var isElevated = _scopeContext.IsSystem;
        var isSelf = _scopeContext.UserId == id;
        if (!isElevated && !isSelf)
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
    /// Ban a user (system only). Revokes all active refresh tokens.
    /// </summary>
    [HttpPost("{id}/ban")]
    [AuthorizePolicy<SystemPolicy>]
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
    /// Unban a user (system only).
    /// </summary>
    [HttpPost("{id}/unban")]
    [AuthorizePolicy<SystemPolicy>]
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

    #region Dummy

    /// <summary>
    /// Dummy user creation request options.
    /// </summary>
    public record CreateDummyUserRequest(int? LifetimeMinutes = null);

    /// <summary>
    /// Create or recycle a dummy user cloning the roles/claims of the specified user (System role only).
    /// Returns credentials and initial authentication tokens (<see cref="Identity.Contracts.Dtos.DummyUserDto"/>).
    /// </summary>
    [HttpPost("{id}/dummy")]
    [AuthorizePolicy<SystemPolicy>]
    public async Task<IActionResult> CreateDummy(
        [FromRoute] DefaultId id,
        [FromBody] CreateDummyUserRequest? body = null,
        CancellationToken ct = default)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new CreateDummyUser(id, _scopeContext.UserId.Value, body?.LifetimeMinutes), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Purge all expired dummy users and their associated tokens/keys (System role only).
    /// </summary>
    [HttpDelete("dummy/expired")]
    [AuthorizePolicy<SystemPolicy>]
    public async Task<IActionResult> PurgeExpiredDummies(CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(
            new PurgeExpiredDummyUsers(), ct);

        return result.Match<IActionResult>(
            purged => Ok(new { purged }),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion
}
