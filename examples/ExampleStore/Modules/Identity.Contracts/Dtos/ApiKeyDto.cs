namespace Identity.Contracts.Dtos;

/// <summary>
/// Data transfer object representing an API key without the secret hash.
/// </summary>
public record ApiKeyDto(
    DefaultId Id,
    string Name,
    string KeyPrefix,
    DefaultId UserId,
    DateTime? ExpiresAt,
    DateTime? RevokedAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt,
    IReadOnlyList<string>? Scopes = null)
{
    /// <summary>
    /// Indicates whether the API key is currently valid and active (not revoked and not expired).
    /// </summary>
    public bool IsActive => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    /// <summary>
    /// Indicates whether the API key has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedAt != null;

    /// <summary>
    /// Indicates whether the API key has passed its expiration timestamp.
    /// </summary>
    public bool IsExpired => ExpiresAt != null && ExpiresAt <= DateTime.UtcNow;
}
