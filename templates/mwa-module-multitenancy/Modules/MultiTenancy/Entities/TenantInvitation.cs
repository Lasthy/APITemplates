using Kernel.Entities;
using MultiTenancy.Contracts.Constants;

namespace MultiTenancy.Entities;

/// <summary>
/// Represents a pending or resolved invitation for an email address to join a tenant.
/// </summary>
public class TenantInvitation : IAuditable
{
    public DefaultId Id { get; set; }
    public DefaultId TenantId { get; set; }

    /// <summary>
    /// Email address of the invited user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Role to assign upon acceptance.
    /// </summary>
    public string Role { get; set; } = TenantRoles.Member;

    /// <summary>
    /// Unique invitation token (URL-safe, cryptographically random).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DefaultId? AcceptedByUserId { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
