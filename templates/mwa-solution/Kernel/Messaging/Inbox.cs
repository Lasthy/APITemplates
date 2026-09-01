using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging;

/// <summary>
/// Entity Framework Core implementation of <see cref="IInbox"/> using <see cref="KernelDbContext"/>.
/// </summary>
public class Inbox(
    KernelDbContext _dbContext,
    ILogger<Inbox> _logger
) : IInbox
{
    /// <inheritdoc />
    public async Task<bool> HasBeenProcessedAsync(DefaultId messageId, CancellationToken ct = default)
    {
        return await _dbContext.InboxEntries
            .AnyAsync(e => e.MessageId == messageId, ct);
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(DefaultId messageId, CancellationToken ct = default)
    {
        try
        {
            var exists = await _dbContext.InboxEntries
                .AnyAsync(e => e.MessageId == messageId, ct);

            if (!exists)
            {
                var entry = new InboxEntry
                {
                    Id = DefaultId.NewUlid(),
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow
                };

                await _dbContext.InboxEntries.AddAsync(entry, ct);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogDebug("Recorded message {MessageId} in inbox.", messageId);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex, "Inbox entry for message {MessageId} already exists or concurrent insert encountered.", messageId);
        }
    }
}
