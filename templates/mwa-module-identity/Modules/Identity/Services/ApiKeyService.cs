using System.Security.Cryptography;
using System.Text;
using Identity.Configuration;
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
    private readonly IdentitySettings _identitySettings;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        IdentityModuleDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IdentitySettings identitySettings,
        IEventBus eventBus,
        ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _identitySettings = identitySettings;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyCreatedDto>> GenerateKeyAsync(
        DefaultId userId,
        string name,
        DateTime? expiresAt = null,
        IReadOnlyList<string>? scopes = null,
        CancellationToken ct = default)
    {
        if (!_identitySettings.EnableApiKeys)
        {
            return Result.Failure<ApiKeyCreatedDto>(new InvalidOperationException("API key creation is disabled."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ApiKeyCreatedDto>(new ArgumentException("API key name is required.", nameof(name)));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return Result.Failure<ApiKeyCreatedDto>(new InvalidOperationException($"User with ID '{userId}' was not found."));
        }

        if (user.IsBanned)
        {
            return Result.Failure<ApiKeyCreatedDto>(new UnauthorizedAccessException("User is banned."));
        }

        // Expiration policy enforcement
        if (_identitySettings.RequireKeyExpiration && expiresAt == null)
        {
            return Result.Failure<ApiKeyCreatedDto>(new ArgumentException("API key expiration date is required."));
        }

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
        {
            return Result.Failure<ApiKeyCreatedDto>(new ArgumentException("API key expiration date must be in the future."));
        }

        if (_identitySettings.MaxApiKeyLifetimeDays.HasValue)
        {
            var maxExpiry = DateTime.UtcNow.AddDays(_identitySettings.MaxApiKeyLifetimeDays.Value);
            if (expiresAt.HasValue && expiresAt.Value > maxExpiry)
            {
                return Result.Failure<ApiKeyCreatedDto>(new ArgumentException(
                    $"API key expiration cannot exceed {_identitySettings.MaxApiKeyLifetimeDays.Value} days (maximum: {maxExpiry:u})."));
            }

            if (!expiresAt.HasValue)
            {
                expiresAt = maxExpiry;
            }
        }

        // Active key quota check
        if (_identitySettings.MaxKeysPerUser > 0)
        {
            var activeKeys = await _dbContext.ApiKeys.CountAsync(
                k => k.UserId == userId && k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow), ct);

            if (activeKeys >= _identitySettings.MaxKeysPerUser)
            {
                return Result.Failure<ApiKeyCreatedDto>(new InvalidOperationException(
                    $"User has reached the maximum number of active API keys ({_identitySettings.MaxKeysPerUser})."));
            }
        }

        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hex = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var rawKey = $"mwa_{hex}";
        var keyPrefix = rawKey[..8];
        var hash = HashKey(rawKey);

        var scopesStr = scopes is { Count: > 0 }
            ? string.Join(",", scopes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
            : null;

        var apiKey = new ApiKey
        {
            Id = DefaultId.NewUlid(),
            Name = name.Trim(),
            KeyHash = hash,
            KeyPrefix = keyPrefix,
            UserId = userId,
            ExpiresAt = expiresAt,
            Scopes = scopesStr,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ApiKeyCreatedEvent(apiKey.Id, userId, apiKey.Name, apiKey.ScopeList), ct);

        return Result.Success(new ApiKeyCreatedDto(
            apiKey.Id,
            apiKey.Name,
            rawKey,
            apiKey.KeyPrefix,
            apiKey.UserId,
            apiKey.ExpiresAt,
            apiKey.CreatedAt,
            apiKey.ScopeList));
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyValidationResult>> ValidateKeyAsync(
        string apiKey,
        CancellationToken ct = default)
    {
        if (!_identitySettings.EnableApiKeys)
        {
            return Result.Failure<ApiKeyValidationResult>(new UnauthorizedAccessException("API key authentication is disabled."));
        }

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
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scopes
            })
            .ToListAsync(ct);

        var dtos = keys.Select(k => new ApiKeyDto(
            k.Id,
            k.Name,
            k.KeyPrefix,
            k.UserId,
            k.ExpiresAt,
            k.RevokedAt,
            k.LastUsedAt,
            k.CreatedAt,
            string.IsNullOrWhiteSpace(k.Scopes) ? null : k.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToList();

        return Result.Success<IEnumerable<ApiKeyDto>>(dtos);
    }

    /// <inheritdoc/>
    public async Task<Result<ApiKeyDto>> GetKeyByIdAsync(
        DefaultId keyId,
        CancellationToken ct = default)
    {
        var key = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.Id == keyId)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt,
                k.Scopes
            })
            .FirstOrDefaultAsync(ct);

        if (key == null)
        {
            return Result.Failure<ApiKeyDto>(new InvalidOperationException($"API key with ID '{keyId}' was not found."));
        }

        var dto = new ApiKeyDto(
            key.Id,
            key.Name,
            key.KeyPrefix,
            key.UserId,
            key.ExpiresAt,
            key.RevokedAt,
            key.LastUsedAt,
            key.CreatedAt,
            string.IsNullOrWhiteSpace(key.Scopes) ? null : key.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return Result.Success(dto);
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
