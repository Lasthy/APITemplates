using Kernel.Entities;
using MultiTenancy.Contracts.Constants;

namespace MultiTenancy.Entities;

/// <summary>
/// Represents a user's membership and role within a tenant organization.
/// </summary>
public class TenantMember : IAuditable
{
    public DefaultId Id { get; set; }
    public DefaultId TenantId { get; set; }
    public DefaultId UserId { get; set; }

    /// <summary>
    /// Role within the tenant: Owner, Admin, Member, ReadOnly.
    /// </summary>
    public string Role { get; set; } = TenantRoles.Member;

    public DateTime JoinedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
