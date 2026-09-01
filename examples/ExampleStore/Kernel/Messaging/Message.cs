namespace Kernel.Messaging;

/// <summary>
/// Represents a message persisted to the database to be processed later. It stores the type and the content, and also
/// keeps track of the attempts, status and errors.
/// </summary>
public class Message
{
	/// <summary>
    /// The message unique identifier.
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

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
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

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

    /// <summary>
    /// Correlation identifier for end-to-end tracing across requests and messages.
    /// </summary>
    public string? CorrelationId { get; set; }
	
	public void IncrementRetryAttempts() {
		RetryCount++;
	}

	/// <summary>
	/// Marks the message as currently being processed.
	/// </summary>
	public void MarkProcessing()
	{
		ProcessingAt = DateTime.UtcNow;
	}

	/// <summary>
	/// Marks the message as successfully processed.
	/// </summary>
	public void MarkProcessed()
	{
		ProcessedAt = DateTime.UtcNow;
		ProcessingAt = null;
		LastError = null;
	}

	/// <summary>
	/// Marks the message as failed, increments retry count, updates error details,
	/// and sets the next retry timestamp based on exponential backoff.
	/// </summary>
	public void MarkFailed(string error, TimeSpan? retryDelay = null, bool maxRetriesReached = false)
	{
		IncrementRetryAttempts();
		ProcessingAt = null;
		LastError = error;
		NextRetryAt = maxRetriesReached ? null : (DateTime.UtcNow + (retryDelay ?? TimeSpan.Zero));
	}

	/// <summary>
	/// Marks the message as timed out from a prior stuck processing attempt,
	/// increments retry count, and schedules retry.
	/// </summary>
	public void MarkTimedOut(TimeSpan? retryDelay = null, bool maxRetriesReached = false)
	{
		IncrementRetryAttempts();
		ProcessingAt = null;
		LastError = $"Message processing timed out after attempt {RetryCount}.";
		NextRetryAt = maxRetriesReached ? null : (DateTime.UtcNow + (retryDelay ?? TimeSpan.Zero));
	}

	/// <summary>
	/// Determines if the message has been in the processing state longer than the specified timeout duration.
	/// </summary>
	public bool IsProcessingTimedOut(TimeSpan timeout)
	{
		return ProcessedAt == null && ProcessingAt.HasValue && ProcessingAt.Value.Add(timeout) < DateTime.UtcNow;
	}
}