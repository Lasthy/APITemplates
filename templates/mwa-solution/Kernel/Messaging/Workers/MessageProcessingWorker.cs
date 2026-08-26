using Kernel.Boxing;
using Kernel.Configuration;
using Kernel.Persistence;
using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging.Workers;

/// <summary>
/// Background worker that periodically polls, claims, and processes pending outbox messages within the instance's partition range.
/// Features partition sharding, stuck/timed-out message recovery, and exponential backoff retry scheduling.
/// </summary>
public class MessageProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageProcessingWorker> logger,
    MessagingSettings messagingSettings
) : BaseWorker(scopeFactory, logger, TimeSpan.FromSeconds(messagingSettings.ProcessingIntervalSeconds))
{
    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Dynamic interval update if changed via DynamicSettings
        Interval = TimeSpan.FromSeconds(messagingSettings.ProcessingIntervalSeconds);

        var db = services.GetRequiredService<KernelDbContext>();
        var dispatcher = services.GetRequiredService<IMessageDispatcher>();
        var instanceSettings = services.GetRequiredService<InstanceSettings>();

        var partitionRange = instanceSettings.Partition;
        var now = DateTime.UtcNow;
        var timeoutCutoff = now.AddSeconds(-messagingSettings.ProcessingTimeoutSeconds);

        // ── 1. Recover stuck / timed-out messages ──────────────────────────
        var timedOutMessages = await db.Messages
            .Where(m => m.ProcessedAt == null
                && m.ProcessingAt != null
                && m.ProcessingAt <= timeoutCutoff
                && m.Partition >= partitionRange.Start
                && m.Partition <= partitionRange.End)
            .ToListAsync(cancellationToken);

        if (timedOutMessages.Count > 0)
        {
            foreach (var message in timedOutMessages)
            {
                var nextRetryCount = message.RetryCount + 1;
                var maxRetriesReached = nextRetryCount >= messagingSettings.MaxRetryAttempts;
                var delay = CalculateExponentialBackoff(
                    nextRetryCount,
                    messagingSettings.InitialRetryDelaySeconds,
                    messagingSettings.MaxRetryDelaySeconds);

                logger.LogWarning(
                    "Message {MessageId} (Channel: {Channel}, Partition: {Partition}) timed out during processing (started at {ProcessingAt}). Attempt {Attempt}/{MaxAttempts}. Next retry in {DelaySeconds}s.",
                    message.Id, message.Channel, message.Partition, message.ProcessingAt, nextRetryCount, messagingSettings.MaxRetryAttempts, delay.TotalSeconds);

                message.MarkTimedOut(delay, maxRetriesReached);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        // ── 2. Query pending messages ready for processing ─────────────────
        var pendingMessages = await db.Messages
            .Where(m => m.ProcessedAt == null
                && m.ProcessingAt == null
                && (m.NextRetryAt == null || m.NextRetryAt <= now)
                && m.RetryCount < messagingSettings.MaxRetryAttempts
                && m.Partition >= partitionRange.Start
                && m.Partition <= partitionRange.End)
            .OrderBy(m => m.OccurredAt)
            .Take(messagingSettings.ProcessingBatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Found {Count} pending message(s) to process in partition range [{Start}..{End}].",
            pendingMessages.Count, partitionRange.Start, partitionRange.End);

        // ── 3. Process each message ─────────────────────────────────────────
        foreach (var message in pendingMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            message.MarkProcessing();
            await db.SaveChangesAsync(cancellationToken);

            var result = await dispatcher.DispatchAsync(message, cancellationToken);

            if (result.IsSuccess)
            {
                message.MarkProcessed();
                logger.LogInformation("Message {MessageId} processed successfully.", message.Id);
            }
            else
            {
                var nextRetryCount = message.RetryCount + 1;
                var maxRetriesReached = nextRetryCount >= messagingSettings.MaxRetryAttempts;
                var delay = CalculateExponentialBackoff(
                    nextRetryCount,
                    messagingSettings.InitialRetryDelaySeconds,
                    messagingSettings.MaxRetryDelaySeconds);
                var errorMessage = result.Error?.Message ?? "Message processing failed without specific error message.";

                message.MarkFailed(errorMessage, delay, maxRetriesReached);

                logger.LogWarning(
                    "Message {MessageId} failed processing on attempt {Attempt}/{MaxAttempts}. Next retry in {DelaySeconds}s. Error: {Error}",
                    message.Id, nextRetryCount, messagingSettings.MaxRetryAttempts, delay.TotalSeconds, errorMessage);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Computes exponential backoff delay based on retry attempt: <c>delay = initialDelay * 2^(retryCount - 1)</c> capped at <c>maxDelaySeconds</c>.
    /// </summary>
    public static TimeSpan CalculateExponentialBackoff(int retryCount, int initialDelaySeconds, int maxDelaySeconds)
    {
        if (retryCount <= 0) return TimeSpan.Zero;

        var exponent = Math.Min(retryCount - 1, 30); // Prevent overflow
        var factor = Math.Pow(2, exponent);
        var delaySeconds = initialDelaySeconds * factor;

        var cappedDelay = Math.Min(delaySeconds, maxDelaySeconds);
        return TimeSpan.FromSeconds(cappedDelay);
    }
}
