using Identity.Contracts.Dtos;
using Identity.Entities;
using Kernel.Common;

namespace Identity.Services;

/// <summary>
/// Result returned when an API key is successfully validated.
/// </summary>
public record ApiKeyValidationResult(
    ApiKey ApiKey,
    ApplicationUser User,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, string> Claims);

/// <summary>
/// Service abstraction for managing API keys (generation, cryptographic hashing, validation, revocation, and lookup).
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key for the specified user with a cryptographically secure random secret.
    /// The secret key is only returned once in the result DTO and is stored in hashed form.
    /// </summary>
    Task<Result<ApiKeyCreatedDto>> GenerateKeyAsync(
        DefaultId userId,
        string name,
        DateTime? expiresAt = null,
        IReadOnlyList<string>? scopes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a raw plaintext API key, verifies expiration, revocation, and user status,
    /// updates <see cref="ApiKey.LastUsedAt"/>, and returns user identity, roles, and claims.
    /// </summary>
    Task<Result<ApiKeyValidationResult>> ValidateKeyAsync(
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    Task<Result> RevokeKeyAsync(
        DefaultId keyId,
        DefaultId requestingUserId,
        bool isManagerOrSystem = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all API keys for a specific user.
    /// </summary>
    Task<Result<IEnumerable<ApiKeyDto>>> GetUserKeysAsync(
        DefaultId userId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets an API key by its unique ID.
    /// </summary>
    Task<Result<ApiKeyDto>> GetKeyByIdAsync(
        DefaultId keyId,
        CancellationToken ct = default);
}
