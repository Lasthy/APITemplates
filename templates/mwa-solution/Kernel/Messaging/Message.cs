namespace Kernel.Boxing;

/// <summary>
/// Represents a message persisted to the database to be processed later. It stores the type and the content, and also
/// keeps track of the attempts, status and errors.
/// </summary>
public class Message
{
	/// <summary>
    /// The message unique identifier.
    /// </summary>
    public DefaultId Id { get; } = DefaultId.NewUlid();

    /// <summary>
    /// CLR type full name of the content.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Serialized payload.
    /// </summary>
    public required string Content { get; init; }
	
	/// <summary>
    /// The channel of the message (ex. email, integration, etc).
    /// </summary>
	public required MessageChannel Channel { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

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
    public int RetryCount { get; private set; }

    /// <summary>
    /// When the message should be retried.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Partition number for sharding concurrent processing.
    /// </summary>
    public required int Partition { get; init; }

	/// <summary>
	/// The error message of the last processing attempt.
	/// </summary>
    public string? LastError { get; set; }
	
	public void IncrementRetryAttempts() {
		RetryCount++;
	}
}