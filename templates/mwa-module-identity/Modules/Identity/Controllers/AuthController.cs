using Identity.Configuration;
using Identity.Features.Auth;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>
/// Authentication endpoints: login, refresh, logout, current user info.
/// </summary>
[ApiController]
[Route("api/v1/identity/auth")]
[ApiExplorerSettings(GroupName = "Identity")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IScopeContext _scopeContext;
    private readonly IdentitySettings _identitySettings;

    public AuthController(
        IDispatcher dispatcher,
        IScopeContext scopeContext,
        IdentitySettings identitySettings)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
        _identitySettings = identitySettings;
    }

    /// <summary>
    /// Authenticate with email and password. Returns JWT access token + refresh token.
    /// Also sets an HttpOnly cookie with the access token if cookie auth is enabled.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new Login(body.Email, body.Password), ct);

        return result.Match<IActionResult>(
            success =>
            {
                if (_identitySettings.EnableCookieAuth)
                {
                    SetAccessTokenCookie(success.AccessToken, success.ExpiresAt);
                }
                return Ok(success);
            },
            failure => Unauthorized(new { message = failure.Message }));
    }

    /// <summary>
    /// Refresh the access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new Features.Auth.RefreshToken(body.Token), ct);

        return result.Match<IActionResult>(
            success =>
            {
                if (_identitySettings.EnableCookieAuth)
                {
                    SetAccessTokenCookie(success.AccessToken, success.ExpiresAt);
                }
                return Ok(success);
            },
            failure => Unauthorized(new { message = failure.Message }));
    }

    /// <summary>
    /// Logout: revoke all refresh tokens for the authenticated user and clear the auth cookie.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new Logout(_scopeContext.UserId.Value), ct);

        // Clear the access token cookie
        if (_identitySettings.EnableCookieAuth)
        {
            Response.Cookies.Delete(_identitySettings.CookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });
        }

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    /// <summary>
    /// Get the currently authenticated user's details.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new GetCurrentUser(_scopeContext.UserId.Value), ct);

        return result.Match<IActionResult>(
            success => Ok(success),
            failure => NotFound(new { message = failure.Message }));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void SetAccessTokenCookie(string accessToken, DateTime expiresAt)
    {
        Response.Cookies.Append(_identitySettings.CookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
        });
    }
}

// ── Request body models ───────────────────────────────────────────────────────

/// <summary>
/// Login request body.
/// </summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Refresh token request body.
/// </summary>
public record RefreshTokenRequest(string Token);
