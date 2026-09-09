namespace Webhooks.Entities;

/// <summary>
/// Represents an individual webhook delivery attempt record.
/// </summary>
public class WebhookDelivery
{
    public DefaultId Id { get; set; }
    public DefaultId SubscriptionId { get; set; }
    public DefaultId WebhookEventId { get; set; }

    /// <summary>
    /// The CLR type name of the event that was delivered.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status: Pending, Succeeded, Failed, DeadLettered.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// HTTP status code returned by the receiver (null if connection failed).
    /// </summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    /// Response body (truncated to max 4KB for logging).
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// The serialized JSON payload that was sent.
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    /// Error message if delivery failed (connection error, timeout, etc.).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Current attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Round-trip duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }

    // Navigation
    public WebhookSubscription Subscription { get; set; } = null!;
}
