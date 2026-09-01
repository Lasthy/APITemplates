using Kernel.Configuration;
using Kernel.Persistence;
using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging.Workers;

/// <summary>
/// Periodic background worker that deletes old processed (and optionally permanently failed) messages and inbox entries after a configured retention period.
/// </summary>
public class MessageCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageCleanupWorker> logger,
    MessagingSettings messagingSettings
) : BaseWorker(scopeFactory, logger, TimeSpan.FromHours(messagingSettings.CleanupIntervalHours))
{
    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Dynamic interval update if changed via DynamicSettings
        Interval = TimeSpan.FromHours(messagingSettings.CleanupIntervalHours);

        var db = services.GetRequiredService<KernelDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-messagingSettings.CleanupRetentionDays);

        logger.LogDebug(
            "MessageCleanupWorker: Checking for messages and inbox entries older than {Days} days (cutoff: {Cutoff}).",
            messagingSettings.CleanupRetentionDays, cutoff);

        // Fetch old processed messages
        var messagesToDelete = await db.Messages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt <= cutoff)
            .ToListAsync(cancellationToken);

        // Optionally include permanently failed messages exceeding max retries
        if (messagingSettings.DeleteFailedMessagesOnCleanup)
        {
            var failedMessages = await db.Messages
                .Where(m => m.ProcessedAt == null
                    && m.RetryCount >= messagingSettings.MaxRetryAttempts
                    && m.OccurredAt <= cutoff)
                .ToListAsync(cancellationToken);

            messagesToDelete.AddRange(failedMessages);
        }

        if (messagesToDelete.Count > 0)
        {
            db.Messages.RemoveRange(messagesToDelete);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "MessageCleanupWorker: Successfully cleaned up {Count} message(s) older than {Days} days.",
                messagesToDelete.Count, messagingSettings.CleanupRetentionDays);
        }
        else
        {
            logger.LogDebug("MessageCleanupWorker: No expired messages found for cleanup.");
        }

        // Fetch and clean up old inbox entries
        var inboxEntriesToDelete = await db.InboxEntries
            .Where(i => i.ProcessedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (inboxEntriesToDelete.Count > 0)
        {
            db.InboxEntries.RemoveRange(inboxEntriesToDelete);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "MessageCleanupWorker: Successfully cleaned up {Count} inbox entry/entries older than {Days} days.",
                inboxEntriesToDelete.Count, messagingSettings.CleanupRetentionDays);
        }
        else
        {
            logger.LogDebug("MessageCleanupWorker: No expired inbox entries found for cleanup.");
        }
    }
}

