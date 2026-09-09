using Kernel.Encryption;
using Kernel.Entities;

namespace Webhooks.Entities;

/// <summary>
/// Represents a webhook subscription that listens for domain events and dispatches HTTP POST payloads.
/// </summary>
public class WebhookSubscription : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable name for this subscription (e.g., "Stripe payment sync").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Target URL to receive webhook POST requests.
    /// Must be HTTPS in production.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 secret used to sign payloads.
    /// Generated on creation, rotatable via endpoint.
    /// </summary>
    [Encrypted]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of event type names this subscription listens to.
    /// Empty/null means "all events" (wildcard).
    /// Example: "UserCreatedEvent,OrderCompletedEvent"
    /// </summary>
    public string? EventTypes { get; set; }

    /// <summary>
    /// Current status: Active, Paused, Disabled (auto-disabled after too many failures).
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Optional description for administrative purposes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional custom HTTP headers to include in deliveries (JSON object).
    /// Example: {"Authorization": "Bearer xyz"}
    /// </summary>
    public string? CustomHeadersJson { get; set; }

    /// <summary>
    /// The user who owns this subscription.
    /// </summary>
    public DefaultId OwnerUserId { get; set; }

    /// <summary>
    /// Consecutive failure count. Used for auto-disable logic.
    /// Resets to 0 on successful delivery.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total successful deliveries (lifetime counter).
    /// </summary>
    public long TotalDeliveries { get; set; }

    /// <summary>
    /// Total failed deliveries (lifetime counter).
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Timestamp of the last successful delivery.
    /// </summary>
    public DateTime? LastDeliveryAt { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }

    // Navigation
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}
