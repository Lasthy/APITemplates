using Cms.Configuration;
using Cms.Persistence;
using Kernel.Storage;
using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cms.Workers;

/// <summary>
/// Background worker that periodically purges orphaned media files (unattached to any content item)
/// that exceed the configured grace period.
/// </summary>
public class MediaCleanupWorker : BaseWorker
{
    private readonly ILogger<MediaCleanupWorker> _logger;

    public MediaCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaCleanupWorker> logger,
        CmsSettings settings)
        : base(scopeFactory, logger, TimeSpan.FromHours(Math.Max(1, settings.OrphanedMediaCleanupIntervalHours)))
    {
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<CmsModuleDbContext>();
        var fileStorage = services.GetRequiredService<IFileStorage>();
        var settings = services.GetRequiredService<CmsSettings>();

        var gracePeriodHours = Math.Max(1, settings.OrphanedMediaGracePeriodHours);
        var cutoff = DateTime.UtcNow.AddHours(-gracePeriodHours);

        var orphanedMedia = await dbContext.Media
            .Where(m => m.ContentItemId == null && m.CreatedAt < cutoff)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (orphanedMedia.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} orphaned media files older than {Cutoff} for cleanup",
            orphanedMedia.Count, cutoff);

        foreach (var media in orphanedMedia)
        {
            try
            {
                await fileStorage.DeleteAsync(media.StoragePath, cancellationToken);
                dbContext.Media.Remove(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up orphaned media file {MediaId} at {Path}",
                    media.Id, media.StoragePath);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orphaned media cleanup completed");
    }
}
