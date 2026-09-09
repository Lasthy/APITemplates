using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Webhooks.Configuration;
using Webhooks.Persistence;

namespace Webhooks.Workers;

/// <summary>
/// Background worker that periodically purges old delivery records and processed webhook events past retention days.
/// </summary>
public class DeliveryCleanupWorker : BaseWorker
{
    private readonly WebhookSettings _settings;
    private readonly ILogger<DeliveryCleanupWorker> _logger;

    public DeliveryCleanupWorker(
        IServiceScopeFactory scopeFactory,
        WebhookSettings settings,
        ILogger<DeliveryCleanupWorker> logger)
        : base(scopeFactory, logger, TimeSpan.FromHours(Math.Max(1, settings.CleanupIntervalHours)))
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Interval = TimeSpan.FromHours(Math.Max(1, _settings.CleanupIntervalHours));

        var dbContext = services.GetRequiredService<WebhookModuleDbContext>();
        var retentionCutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _settings.CleanupRetentionDays));

        // 1. Purge old deliveries past retention days
        var oldDeliveries = await dbContext.Deliveries
            .Where(d => d.CreatedAt < retentionCutoff)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (oldDeliveries.Count > 0)
        {
            dbContext.Deliveries.RemoveRange(oldDeliveries);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("DeliveryCleanupWorker purged {Count} deliveries older than {Cutoff}.", oldDeliveries.Count, retentionCutoff);
        }

        // 2. Purge processed events past retention days
        var oldEvents = await dbContext.Events
            .Where(e => e.IsProcessed && e.OccurredAt < retentionCutoff)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (oldEvents.Count > 0)
        {
            dbContext.Events.RemoveRange(oldEvents);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("DeliveryCleanupWorker purged {Count} processed events older than {Cutoff}.", oldEvents.Count, retentionCutoff);
        }
    }
}
