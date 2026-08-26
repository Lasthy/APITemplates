using Identity.Configuration;
using Identity.Contracts.Dtos;
using Identity.Entities;
using Identity.Persistence;
using Identity.Profiles;
using Kernel.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Services;

/// <summary>
/// Implementation of <see cref="IUserService"/> using ASP.NET Identity and the profile system.
/// </summary>
public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ProfileRegistry _profileRegistry;
    private readonly ITokenService _tokenService;
    private readonly IdentitySettings _identitySettings;
    private readonly IdentityModuleDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ProfileRegistry profileRegistry,
        ITokenService tokenService,
        IdentitySettings identitySettings,
        IdentityModuleDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _profileRegistry = profileRegistry;
        _tokenService = tokenService;
        _identitySettings = identitySettings;
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ApplicationUser>> CreateUserAsync(
        string email,
        string userName,
        string password,
        string profileName,
        CancellationToken ct = default)
    {
        var profile = _profileRegistry.Get(profileName);
        if (profile == null)
        {
            return Result.Failure<ApplicationUser>(
                new InvalidOperationException($"Profile '{profileName}' is not registered."));
        }

        var user = new ApplicationUser
        {
            Id = DefaultId.NewUlid(),
            Email = email,
            UserName = userName,
            ProfileName = profileName
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result.Failure<ApplicationUser>(
                new InvalidOperationException($"Failed to create user: {errors}"));
        }

        // Assign profile roles
        if (profile.Roles.Count > 0)
        {
            var roleResult = await _userManager.AddToRolesAsync(user, profile.Roles);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to assign roles to user {UserId}: {Errors}",
                    user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        // Assign profile claims
        foreach (var (type, value) in profile.Claims)
        {
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(type, value));
        }

        // Run profile creation hook
        var context = new ProfileCreationContext
        {
            User = user,
            UserManager = _userManager,
            Services = _serviceProvider
        };

        await profile.OnUserCreated(context, ct);

        _logger.LogInformation("User {UserId} created with profile '{ProfileName}'", user.Id, profileName);

        return Result.Success(user);
    }

    /// <inheritdoc/>
    public async Task<Result<AuthTokenDto>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("Invalid email or password."));
        }

        if (user.IsBanned)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("This account has been banned."));
        }

        if (user.IsDummy)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("Dummy accounts cannot authenticate."));
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("Invalid email or password."));
        }

        return await GenerateTokensForUserAsync(user, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<AuthTokenDto>> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        var storedToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (storedToken == null || !storedToken.IsActive)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("Invalid or expired refresh token."));
        }

        if (storedToken.User.IsBanned)
        {
            return Result.Failure<AuthTokenDto>(
                new UnauthorizedAccessException("This account has been banned."));
        }

        // Revoke the old refresh token
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByToken = _tokenService.GenerateRefreshToken();

        // Create new refresh token
        var newRefreshToken = new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = storedToken.ReplacedByToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_identitySettings.RefreshTokenLifetimeDays)
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(ct);

        // Generate new access token
        var user = storedToken.User;
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles, claims);

        return Result.Success(new AuthTokenDto(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken.Token,
            ExpiresAt: _tokenService.GetAccessTokenExpiration()));
    }

    /// <inheritdoc/>
    public async Task<Result> RevokeAllRefreshTokensAsync(
        DefaultId userId,
        CancellationToken ct = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}",
            activeTokens.Count, userId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> BanUserAsync(
        DefaultId userId,
        DefaultId bannedBy,
        string? reason,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(new InvalidOperationException($"User '{userId}' not found."));
        }

        user.IsBanned = true;
        user.BannedAt = DateTime.UtcNow;
        user.BannedBy = bannedBy;
        user.BanReason = reason;

        await _userManager.UpdateAsync(user);

        // Revoke all active refresh tokens
        await RevokeAllRefreshTokensAsync(userId, ct);

        _logger.LogInformation("User {UserId} banned by {BannedBy}. Reason: {Reason}",
            userId, bannedBy, reason ?? "(none)");

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> UnbanUserAsync(
        DefaultId userId,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(new InvalidOperationException($"User '{userId}' not found."));
        }

        user.IsBanned = false;
        user.BannedAt = null;
        user.BannedBy = null;
        user.BanReason = null;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} unbanned", userId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<AuthTokenDto>> CreateDummyUserAsync(
        DefaultId sourceUserId,
        DefaultId createdBy,
        CancellationToken ct = default)
    {
        var sourceUser = await _userManager.FindByIdAsync(sourceUserId.ToString());
        if (sourceUser == null)
        {
            return Result.Failure<AuthTokenDto>(
                new InvalidOperationException($"Source user '{sourceUserId}' not found."));
        }

        var dummyId = DefaultId.NewUlid();
        var dummyUser = new ApplicationUser
        {
            Id = dummyId,
            Email = $"dummy-{dummyId}@localhost",
            UserName = $"dummy-{dummyId}",
            ProfileName = sourceUser.ProfileName,
            IsDummy = true,
            CreatedBy = createdBy
        };

        var createResult = await _userManager.CreateAsync(dummyUser, $"Dummy!{Guid.NewGuid()}");
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result.Failure<AuthTokenDto>(
                new InvalidOperationException($"Failed to create dummy user: {errors}"));
        }

        // Copy roles from source user
        var sourceRoles = await _userManager.GetRolesAsync(sourceUser);
        if (sourceRoles.Count > 0)
        {
            await _userManager.AddToRolesAsync(dummyUser, sourceRoles);
        }

        // Copy claims from source user
        var sourceClaims = await _userManager.GetClaimsAsync(sourceUser);
        foreach (var claim in sourceClaims)
        {
            await _userManager.AddClaimAsync(dummyUser, claim);
        }

        _logger.LogInformation(
            "Dummy user {DummyUserId} created by {CreatedBy}, cloning user {SourceUserId}",
            dummyId, createdBy, sourceUserId);

        // Generate tokens directly for the dummy user
        return await GenerateTokensForUserAsync(dummyUser, ct);
    }

    /// <inheritdoc/>
    public async Task<Result> ChangePasswordAsync(
        DefaultId userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(new InvalidOperationException($"User '{userId}' not found."));
        }

        if (user.IsBanned)
        {
            return Result.Failure(new UnauthorizedAccessException("This account has been banned."));
        }

        if (user.IsDummy)
        {
            return Result.Failure(new InvalidOperationException("Cannot change password for dummy accounts."));
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            var errors = string.Join("; ", changeResult.Errors.Select(e => e.Description));
            return Result.Failure(new InvalidOperationException($"Failed to change password: {errors}"));
        }

        // Revoke all active refresh tokens on password change for security
        await RevokeAllRefreshTokensAsync(userId, ct);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<(ApplicationUser User, string Token)?>> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsBanned || user.IsDummy)
        {
            return Result.Success<(ApplicationUser User, string Token)?>(null);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        _logger.LogInformation("Generated password reset token for user {UserId}", user.Id);

        return Result.Success<(ApplicationUser User, string Token)?>((user, token));
    }

    /// <inheritdoc/>
    public async Task<Result<ApplicationUser>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsBanned || user.IsDummy)
        {
            return Result.Failure<ApplicationUser>(
                new InvalidOperationException("Invalid password reset request."));
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            return Result.Failure<ApplicationUser>(
                new InvalidOperationException($"Failed to reset password: {errors}"));
        }

        // Revoke all active refresh tokens on password reset for security
        await RevokeAllRefreshTokensAsync(user.Id, ct);

        _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);

        return Result.Success(user);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Result<AuthTokenDto>> GenerateTokensForUserAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles, claims);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(_identitySettings.RefreshTokenLifetimeDays)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(new AuthTokenDto(
            AccessToken: accessToken,
            RefreshToken: refreshTokenStr,
            ExpiresAt: _tokenService.GetAccessTokenExpiration()));
    }
}
