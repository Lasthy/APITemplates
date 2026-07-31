namespace Kernel.Boxing;

/// <summary>
/// Represents an inbox/outbox message persisted to the database
/// </summary>
public class Message
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// CLR type full name of the event.
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Serialized event payload
    /// </summary>
    public string Content { get; set; } = null!;
	
	/// <summary>
    /// The channel of the message (ex. email, integration, etc)
    /// </summary>
	public MessageChannel Channel { get; set; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// When the message was successfully processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// When the message was claimed for processing.
    /// </summary>
    public DateTime? ProcessingAt { get; set; }

    /// <summary>
    /// Number of times processing has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the message should be retried.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Partition number for sharding concurrent processing.
    /// </summary>
    public int Partition { get; set; }

    public string? LastError { get; set; }
}