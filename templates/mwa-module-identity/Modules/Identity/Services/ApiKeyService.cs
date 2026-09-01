using System.Security.Cryptography;
using System.Text;
using Identity.Contracts.Dtos;
using Identity.Contracts.Events;
using Identity.Entities;
using Identity.Persistence;
using Kernel.Common;
using Kernel.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Services;

/// <summary>
/// Default implementation of <see cref="IApiKeyService"/>.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IdentityModuleDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        IdentityModuleDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IEventBus _eventBus,
        ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        this._eventBus = _eventBus;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyCreatedDto>> GenerateKeyAsync(
        DefaultId userId,
        string name,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ApiKeyCreatedDto>(new ArgumentException("API key name is required.", nameof(name)));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return Result.Failure<ApiKeyCreatedDto>(new InvalidOperationException($"User with ID '{userId}' was not found."));
        }

        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hex = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var rawKey = $"mwa_{hex}";
        var keyPrefix = rawKey[..8];
        var hash = HashKey(rawKey);

        var apiKey = new ApiKey
        {
            Id = DefaultId.NewUlid(),
            Name = name.Trim(),
            KeyHash = hash,
            KeyPrefix = keyPrefix,
            UserId = userId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ApiKeyCreatedEvent(apiKey.Id, userId, apiKey.Name), ct);

        return Result.Success(new ApiKeyCreatedDto(
            apiKey.Id,
            apiKey.Name,
            rawKey,
            apiKey.KeyPrefix,
            apiKey.UserId,
            apiKey.ExpiresAt,
            apiKey.CreatedAt));
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyValidationResult>> ValidateKeyAsync(
        string apiKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Result.Failure<ApiKeyValidationResult>(new ArgumentException("API key is required.", nameof(apiKey)));
        }

        var hash = HashKey(apiKey.Trim());

        var key = await _dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash, ct);

        if (key == null)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("Invalid API key."));
        }

        if (key.RevokedAt != null)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("API key has been revoked."));
        }

        if (key.ExpiresAt != null && key.ExpiresAt <= DateTime.UtcNow)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("API key has expired."));
        }

        if (key.User == null)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("Associated user not found."));
        }

        if (key.User.IsBanned)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("Associated user is banned."));
        }

        // Update LastUsedAt timestamp
        try
        {
            key.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update LastUsedAt timestamp for API key {ApiKeyId}", key.Id);
        }

        var roles = await _userManager.GetRolesAsync(key.User);
        var claims = await _userManager.GetClaimsAsync(key.User);
        var claimsDict = claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.First().Value);

        return Result.Success(new ApiKeyValidationResult(
            key,
            key.User,
            roles.ToList().AsReadOnly(),
            claimsDict.AsReadOnly()));
    }

    /// <inheritdoc/>
    public async Task<Result> RevokeKeyAsync(
        DefaultId keyId,
        DefaultId requestingUserId,
        bool isManagerOrSystem = false,
        CancellationToken ct = default)
    {
        var key = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId, ct);
        if (key == null)
        {
            return Result.Failure(new InvalidOperationException($"API key with ID '{keyId}' was not found."));
        }

        if (!isManagerOrSystem && key.UserId != requestingUserId)
        {
            return Result.Failure(new UnauthorizedAccessException("Not authorized to revoke this API key."));
        }

        if (key.RevokedAt != null)
        {
            return Result.Success();
        }

        key.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ApiKeyRevokedEvent(key.Id, key.UserId, key.Name), ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<ApiKeyDto>>> GetUserKeysAsync(
        DefaultId userId,
        CancellationToken ct = default)
    {
        var keys = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<IEnumerable<ApiKeyDto>>(keys);
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyDto>> GetKeyByIdAsync(
        DefaultId keyId,
        CancellationToken ct = default)
    {
        var key = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.Id == keyId)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt))
            .FirstOrDefaultAsync(ct);

        if (key == null)
        {
            return Result.Failure<ApiKeyDto>(new InvalidOperationException($"API key with ID '{keyId}' was not found."));
        }

        return Result.Success(key);
    }

    /// <summary>
    /// Computes the SHA-256 hex string hash of the raw plain API key.
    /// </summary>
    public static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
