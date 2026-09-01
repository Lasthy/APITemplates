using Kernel.Entities;

namespace Identity.Entities;

/// <summary>
/// Represents an API key for machine-to-machine, CLI, or integration access.
/// Stores only the cryptographic SHA-256 hash of the plain key.
/// </summary>
public class ApiKey : IAuditable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// Human-readable label / description for the API key.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Hex-encoded SHA-256 hash of the plain API key secret.
    /// </summary>
    public required string KeyHash { get; init; }

    /// <summary>
    /// First 8 characters of the raw API key used for identification and listing.
    /// </summary>
    public required string KeyPrefix { get; init; }

    /// <summary>
    /// User identifier that owns this API key. The API key inherits the roles and claims of this user.
    /// </summary>
    public required DefaultId UserId { get; init; }

    /// <summary>
    /// Optional expiration timestamp. When reached, authentication attempts will be rejected.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the key was revoked. When set, authentication attempts will be rejected.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Timestamp when the API key was last used to successfully authenticate a request.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Navigation property to the owner user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    // ── IAuditable ─────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
