using Kernel.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scheduling.Configuration;
using Scheduling.Contracts.Constants;
using Scheduling.Persistence;
using Scheduling.Services;

namespace Scheduling.Workers;

/// <summary>
/// Background worker that polls for due scheduled jobs, acquires distributed locks, and executes them.
/// </summary>
public class JobSchedulerWorker : BaseWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulingSettings _settings;
    private readonly ILogger<JobSchedulerWorker> _logger;
    private bool _missedFiresHandled = false;

    public JobSchedulerWorker(
        IServiceScopeFactory scopeFactory,
        SchedulingSettings settings,
        ILogger<JobSchedulerWorker> logger)
        : base(scopeFactory, logger, TimeSpan.FromSeconds(Math.Max(1, settings.PollingIntervalSeconds)))
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteJobAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Update polling interval dynamically if settings were updated
        Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.PollingIntervalSeconds));

        var dbContext = services.GetRequiredService<SchedulingModuleDbContext>();
        var scheduler = services.GetRequiredService<IJobScheduler>();

        // Handle missed fire policies on initial startup run
        if (!_missedFiresHandled)
        {
            _missedFiresHandled = true;
            try
            {
                await scheduler.HandleMissedFiresAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred handling missed-fire jobs on startup.");
            }
        }

        var instanceId = scheduler.GetInstanceId();

        // Check active jobs concurrency limit
        var activeLocks = await dbContext.Locks
            .CountAsync(l => l.LockedByInstance == instanceId && l.ExpiresAt > DateTime.UtcNow, cancellationToken);

        var availableSlots = _settings.MaxConcurrentJobs - activeLocks;
        if (availableSlots <= 0)
        {
            _logger.LogDebug("Max concurrent jobs ({Max}) reached on instance {InstanceId}. Skipping scheduler tick.", _settings.MaxConcurrentJobs, instanceId);
            return;
        }

        // Query due jobs ordered by priority and next run time
        var now = DateTime.UtcNow;
        var dueJobIds = await dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.Status == JobStatus.Enabled &&
                        j.DeletedAt == null &&
                        j.NextRunAt != null &&
                        j.NextRunAt <= now)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.NextRunAt)
            .Select(j => j.Id)
            .Take(availableSlots)
            .ToListAsync(cancellationToken);

        if (dueJobIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} due job(s) to execute on instance {InstanceId}.", dueJobIds.Count, instanceId);

        // Execute due jobs concurrently up to available slots
        var executionTasks = dueJobIds.Select(jobId => Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedScheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
                await scopedScheduler.ExecuteJobAsync(jobId, isManualTrigger: false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing due job {JobId}.", jobId);
            }
        }, cancellationToken));

        await Task.WhenAll(executionTasks);
    }
}
