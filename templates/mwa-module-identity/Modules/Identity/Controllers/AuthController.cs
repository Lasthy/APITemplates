using Asp.Versioning;
using Identity.Configuration;
using Identity.Features.Auth;
using Kernel.Authorization;
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
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/auth")]
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

    #region Login

    /// <summary>
    /// Login request body.
    /// </summary>
    public record LoginRequest(string Email, string Password);

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

    #endregion

    #region Refresh

    /// <summary>
    /// Refresh token request body.
    /// </summary>
    public record RefreshTokenRequest(string Token);

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

    #endregion

    #region Logout

    /// <summary>
    /// Logout: revoke all refresh tokens for the authenticated user and clear the auth cookie.
    /// </summary>
    [HttpPost("logout")]
    [AuthorizePolicy<AuthenticatedPolicy>]
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
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax
            });
        }

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region Me

    /// <summary>
    /// Get the currently authenticated user's details.
    /// </summary>
    [HttpGet("me")]
    [AuthorizePolicy<AuthenticatedPolicy>]
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

    #endregion

    #region ChangePassword

    /// <summary>
    /// Change password request body.
    /// </summary>
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    /// <summary>
    /// Change password for the currently authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [AuthorizePolicy<AuthenticatedPolicy>]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest body,
        CancellationToken ct)
    {
        if (_scopeContext.UserId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.DispatchAsync(
            new Features.Auth.ChangePassword(
                _scopeContext.UserId.Value,
                body.CurrentPassword,
                body.NewPassword), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region ForgotPassword

    /// <summary>
    /// Forgot password request body.
    /// </summary>
    public record ForgotPasswordRequest(string Email);

    /// <summary>
    /// Initiate a password reset by sending a reset token event for the specified email.
    /// Always returns 200 OK to prevent email enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new Features.Auth.ForgotPassword(body.Email), ct);

        return result.Match<IActionResult>(
            _ => Ok(new { message = "If the email is registered, a password reset link has been sent." }),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region ResetPassword

    /// <summary>
    /// Reset password request body.
    /// </summary>
    public record ResetPasswordRequest(string Email, string Token, string NewPassword);

    /// <summary>
    /// Reset password using a valid reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest body,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(
            new Features.Auth.ResetPassword(body.Email, body.Token, body.NewPassword), ct);

        return result.Match<IActionResult>(
            _ => Ok(new { message = "Password reset successfully. You can now log in with your new password." }),
            failure => BadRequest(new { message = failure.Message }));
    }

    #endregion

    #region Helpers

    private void SetAccessTokenCookie(string accessToken, DateTime expiresAt)
    {
        Response.Cookies.Append(_identitySettings.CookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
        });
    }

    #endregion
}
