namespace Webhooks.Entities;

/// <summary>
/// Represents a queued domain event waiting to be fanned out to matching webhook subscriptions.
/// </summary>
public class WebhookEvent
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// CLR type name of the original domain event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON of the original domain event.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Whether this event has been fully processed (delivered or enqueued to all matching subscriptions).
    /// </summary>
    public bool IsProcessed { get; set; }

    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
