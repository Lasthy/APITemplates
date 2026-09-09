using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scheduling.Configuration;
using Scheduling.Persistence;

namespace Scheduling.Workers;

/// <summary>
/// Background worker that periodically cleans up aged job execution history and stale distributed locks.
/// </summary>
public class ExecutionCleanupWorker : BaseWorker
{
    private readonly SchedulingSettings _settings;
    private readonly ILogger<ExecutionCleanupWorker> _logger;

    public ExecutionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        SchedulingSettings settings,
        ILogger<ExecutionCleanupWorker> logger)
        : base(scopeFactory, logger, TimeSpan.FromHours(Math.Max(1, settings.CleanupIntervalHours)))
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Interval = TimeSpan.FromHours(Math.Max(1, _settings.CleanupIntervalHours));

        var dbContext = services.GetRequiredService<SchedulingModuleDbContext>();

        // 1. Purge executions past retention days
        var retentionCutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _settings.ExecutionRetentionDays));
        var oldExecutions = await dbContext.Executions
            .Where(e => e.CompletedAt != null && e.CompletedAt < retentionCutoff)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (oldExecutions.Count > 0)
        {
            dbContext.Executions.RemoveRange(oldExecutions);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("ExecutionCleanupWorker purged {Count} historical execution records older than {RetentionCutoff}.", oldExecutions.Count, retentionCutoff);
        }

        // 2. Clean up expired locks older than 5 minutes
        var lockCutoff = DateTime.UtcNow.AddMinutes(-5);
        var expiredLocks = await dbContext.Locks
            .Where(l => l.ExpiresAt < lockCutoff)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (expiredLocks.Count > 0)
        {
            dbContext.Locks.RemoveRange(expiredLocks);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("ExecutionCleanupWorker cleaned up {Count} expired distributed lock records.", expiredLocks.Count);
        }
    }
}
