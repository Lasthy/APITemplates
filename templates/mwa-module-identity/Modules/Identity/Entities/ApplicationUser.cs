using Kernel.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.Entities;

/// <summary>
/// Application user entity extending ASP.NET Identity with profile-based
/// registration, user preferences, and ban/dummy support.
/// Uses <see cref="DefaultId"/> (Ulid) as the primary key type.
/// </summary>
public class ApplicationUser : IdentityUser<DefaultId>, IAuditable
{
    /// <summary>
    /// The profile name this user was created with (e.g., "User", "Admin").
    /// Maps to a registered <see cref="Profiles.IProfile"/> implementation.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON blob storing user preferences.
    /// Schema is optionally validated by the profile's <c>PreferencesType</c>.
    /// </summary>
    public string? PreferencesJson { get; set; }

    /// <summary>
    /// Whether this user is banned. Banned users cannot authenticate.
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// Timestamp when the user was banned, if applicable.
    /// </summary>
    public DateTime? BannedAt { get; set; }

    /// <summary>
    /// The user ID of the admin who banned this user.
    /// </summary>
    public DefaultId? BannedBy { get; set; }

    /// <summary>
    /// Optional reason for the ban.
    /// </summary>
    public string? BanReason { get; set; }

    /// <summary>
    /// Whether this is a dummy user created by an admin for testing/impersonation.
    /// Dummy users cannot authenticate once expired, and are recycled on subsequent requests.
    /// </summary>
    public bool IsDummy { get; set; }

    /// <summary>
    /// Expiration timestamp for dummy users. Once expired, the dummy user cannot authenticate
    /// and is eligible to be recycled when a new dummy user is requested.
    /// </summary>
    public DateTime? DummyExpiresAt { get; set; }

    // ── IAuditable ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DefaultId? CreatedBy { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    /// <inheritdoc/>
    public DefaultId? UpdatedBy { get; set; }
}
