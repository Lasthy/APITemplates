namespace Kernel.Messaging;

/// <summary>
/// Represents a persistent record of a processed message in the inbox to guarantee idempotency and prevent duplicate side effects.
/// </summary>
public class InboxEntry
{
    /// <summary>
    /// Unique identifier of the inbox entry.
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// Identifier of the message that was processed.
    /// </summary>
    public DefaultId MessageId { get; set; }

    /// <summary>
    /// UTC timestamp when the message was marked as processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
