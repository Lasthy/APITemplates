namespace Kernel.Messaging;

/// <summary>
/// Abstraction for managing inbox entries to guarantee idempotency and prevent duplicate message processing.
/// </summary>
public interface IInbox
{
    /// <summary>
    /// Checks whether a message with the specified identifier has already been recorded as processed.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the message has already been processed; otherwise, <c>false</c>.</returns>
    Task<bool> HasBeenProcessedAsync(DefaultId messageId, CancellationToken ct = default);

    /// <summary>
    /// Records a message identifier in the inbox, marking it as processed.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkProcessedAsync(DefaultId messageId, CancellationToken ct = default);
}
