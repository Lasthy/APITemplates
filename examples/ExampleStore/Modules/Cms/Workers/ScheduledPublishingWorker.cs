using Cms.Configuration;
using Cms.Contracts.Constants;
using Cms.Contracts.Events;
using Cms.Persistence;
using Kernel.Events;
using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cms.Workers;

/// <summary>
/// Background worker that periodically checks for draft content items with a past PublishAt date
/// and transitions them to Published.
/// </summary>
public class ScheduledPublishingWorker : BaseWorker
{
    private readonly ILogger<ScheduledPublishingWorker> _logger;

    public ScheduledPublishingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledPublishingWorker> logger,
        CmsSettings settings)
        : base(scopeFactory, logger, TimeSpan.FromSeconds(Math.Max(5, settings.ScheduledPublishingIntervalSeconds)))
    {
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var settings = services.GetRequiredService<CmsSettings>();
        if (!settings.EnableScheduledPublishing)
        {
            return;
        }

        var dbContext = services.GetRequiredService<CmsModuleDbContext>();
        var eventBus = services.GetRequiredService<IEventBus>();

        var now = DateTime.UtcNow;
        var dueItems = await dbContext.ContentItems
            .Where(ci => ci.Status == ContentStatus.Draft && ci.PublishAt != null && ci.PublishAt <= now)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (dueItems.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} scheduled content items to auto-publish", dueItems.Count);

        foreach (var item in dueItems)
        {
            item.Status = ContentStatus.Published;
            item.PublishedAt = now;
            item.UpdatedAt = now;

            await eventBus.PublishAsync(new ContentPublishedEvent(
                item.Id,
                item.ContentType,
                item.Slug,
                item.AuthorId), cancellationToken);

            _logger.LogInformation("Auto-published scheduled content item {ContentId} ({Slug})", item.Id, item.Slug);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
