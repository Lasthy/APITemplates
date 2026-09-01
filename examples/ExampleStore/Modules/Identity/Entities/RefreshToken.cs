namespace Identity.Entities;

/// <summary>
/// Refresh token entity for JWT token rotation.
/// Each refresh token is single-use; on refresh, the old token is revoked
/// and a new one is issued (rotation pattern).
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// The user this refresh token belongs to.
    /// </summary>
    public DefaultId UserId { get; set; }

    /// <summary>
    /// The opaque token string (cryptographic random).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When this refresh token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this refresh token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this refresh token was revoked (null if still active).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// If this token was rotated, the new token string that replaced it.
    /// </summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// Whether this token has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedAt != null;

    /// <summary>
    /// Whether this token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Whether this token is still valid (not revoked and not expired).
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;
}
