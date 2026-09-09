using Kernel.Entities;
using MultiTenancy.Contracts.Constants;

namespace MultiTenancy.Entities;

/// <summary>
/// Represents a tenant organization (e.g., company, team, workspace).
/// </summary>
public class Tenant : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable display name (e.g., "Acme Corporation").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique identifier derived from name (e.g., "acme-corporation").
    /// Used in subdomain and route-segment resolution strategies.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Current lifecycle status: Active, Suspended, or Deleted.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Optional plan/tier identifier for billing or feature gating (e.g., "free", "pro", "enterprise").
    /// </summary>
    public string? Plan { get; set; }

    /// <summary>
    /// Freeform JSON metadata blob for tenant-specific configuration.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// The user who created/owns this tenant. Set on creation, transferable.
    /// </summary>
    public DefaultId OwnerUserId { get; set; }

    /// <summary>
    /// Optional reason for suspension.
    /// </summary>
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DefaultId? SuspendedBy { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
