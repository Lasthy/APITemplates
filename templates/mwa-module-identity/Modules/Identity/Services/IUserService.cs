using Identity.Contracts.Dtos;
using Identity.Entities;
using Kernel.Common;

namespace Identity.Services;

/// <summary>
/// High-level service for user operations: creation, authentication, banning, and dummy users.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Creates a new user with the specified profile, applying all profile-defined
    /// roles, claims, and creation hooks.
    /// </summary>
    Task<Result<ApplicationUser>> CreateUserAsync(
        string email,
        string userName,
        string password,
        string profileName,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new user with the specified profile and additional data, applying all
    /// profile-defined roles, claims, and creation hooks.
    /// </summary>
    Task<Result<ApplicationUser>> CreateUserAsync(
        string email,
        string userName,
        string password,
        string profileName,
        IReadOnlyDictionary<string, object>? additionalData,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates a user by email and password.
    /// Returns an <see cref="AuthTokenDto"/> with JWT access token and refresh token.
    /// </summary>
    Task<Result<AuthTokenDto>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken ct = default);

    /// <summary>
    /// Refreshes an access token using a valid refresh token.
    /// The old refresh token is revoked and a new one is issued (rotation).
    /// </summary>
    Task<Result<AuthTokenDto>> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes all active refresh tokens for the specified user.
    /// </summary>
    Task<Result> RevokeAllRefreshTokensAsync(
        DefaultId userId,
        CancellationToken ct = default);

    /// <summary>
    /// Bans a user, revoking all refresh tokens.
    /// </summary>
    Task<Result> BanUserAsync(
        DefaultId userId,
        DefaultId bannedBy,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Lifts a ban on a user.
    /// </summary>
    Task<Result> UnbanUserAsync(
        DefaultId userId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or recycles a dummy user cloning the roles and claims of the specified source user.
    /// Returns credentials and initial authentication tokens for the dummy user.
    /// </summary>
    Task<Result<DummyUserDto>> CreateDummyUserAsync(
        DefaultId sourceUserId,
        DefaultId createdBy,
        int? lifetimeMinutes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Purges all expired dummy users and their associated tokens and API keys.
    /// Returns the number of purged dummy users.
    /// </summary>
    Task<Result<int>> PurgeExpiredDummyUsersAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Changes the password for the specified user after verifying the current password.
    /// Also revokes all active refresh tokens for security.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        DefaultId userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a password reset token for the user with the given email.
    /// Returns the user entity and the generated token string, or null if the user is not found or banned.
    /// </summary>
    Task<Result<(ApplicationUser User, string Token)?>> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken ct = default);

    /// <summary>
    /// Resets a user's password using the provided reset token.
    /// Also revokes all active refresh tokens for security.
    /// </summary>
    Task<Result<ApplicationUser>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken ct = default);
}
