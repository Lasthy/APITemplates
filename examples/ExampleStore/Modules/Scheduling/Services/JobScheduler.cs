using Kernel.Common;
using Kernel.Configuration;
using Kernel.Context;
using Kernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scheduling.Configuration;
using Scheduling.Contracts.Constants;
using Scheduling.Contracts.Dtos;
using Scheduling.Contracts.Events;
using Scheduling.Cron;
using Scheduling.Entities;
using Scheduling.Handlers;
using Scheduling.Persistence;
using System.Diagnostics;

namespace Scheduling.Services;

/// <summary>
/// Core implementation of the scheduled job engine.
/// </summary>
public class JobScheduler : IJobScheduler
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly IJobLockService _lockService;
    private readonly ICronParser _cronParser;
    private readonly IJobHandlerRegistry _registry;
    private readonly SchedulingSettings _settings;
    private readonly IEventBus _eventBus;
    private readonly IScopeContext _scopeContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobScheduler> _logger;
    private readonly InstanceSettings? _instanceSettings;

    public JobScheduler(
        SchedulingModuleDbContext dbContext,
        IJobLockService lockService,
        ICronParser cronParser,
        IJobHandlerRegistry registry,
        SchedulingSettings settings,
        IEventBus eventBus,
        IScopeContext scopeContext,
        IServiceScopeFactory scopeFactory,
        ILogger<JobScheduler> logger,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _lockService = lockService;
        _cronParser = cronParser;
        _registry = registry;
        _settings = settings;
        _eventBus = eventBus;
        _scopeContext = scopeContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _instanceSettings = serviceProvider.GetService<InstanceSettings>();
    }

    public string GetInstanceId()
    {
        var partitionId = _instanceSettings?.Partition?.Start ?? 0;
        return $"inst_{Environment.MachineName}_{partitionId}";
    }

    public async Task<Result<ScheduledJobDetailDto>> CreateJobAsync(
        string name,
        string handlerType,
        string? cronExpression = null,
        DateTime? scheduledAt = null,
        string? payloadJson = null,
        string? description = null,
        int? timeoutSeconds = null,
        int? maxConsecutiveFailures = null,
        int priority = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ScheduledJobDetailDto>(new ArgumentException("Job name cannot be empty.", nameof(name)));
        }

        if (string.IsNullOrWhiteSpace(handlerType))
        {
            return Result.Failure<ScheduledJobDetailDto>(new ArgumentException("Handler type cannot be empty.", nameof(handlerType)));
        }

        var normalizedName = name.Trim();
        var exists = await _dbContext.Jobs
            .AnyAsync(j => j.Name == normalizedName && j.DeletedAt == null, ct);

        if (exists)
        {
            return Result.Failure<ScheduledJobDetailDto>(new InvalidOperationException($"A job with name '{normalizedName}' already exists."));
        }

        DateTime? nextRunAt;
        bool isOneShot;

        if (!string.IsNullOrWhiteSpace(cronExpression))
        {
            var trimmedCron = cronExpression.Trim();
            if (!_cronParser.IsValid(trimmedCron))
            {
                return Result.Failure<ScheduledJobDetailDto>(new ArgumentException($"Invalid cron expression: '{cronExpression}'.", nameof(cronExpression)));
            }

            isOneShot = false;
            nextRunAt = _cronParser.GetNextOccurrence(trimmedCron, DateTime.UtcNow);
        }
        else if (scheduledAt.HasValue)
        {
            isOneShot = true;
            nextRunAt = scheduledAt.Value.ToUniversalTime();
        }
        else
        {
            return Result.Failure<ScheduledJobDetailDto>(new ArgumentException("Either CronExpression or ScheduledAt must be provided."));
        }

        var job = new ScheduledJob
        {
            Id = DefaultId.NewUlid(),
            Name = normalizedName,
            Description = description?.Trim(),
            HandlerType = handlerType.Trim(),
            CronExpression = string.IsNullOrWhiteSpace(cronExpression) ? null : cronExpression.Trim(),
            ScheduledAt = scheduledAt?.ToUniversalTime(),
            PayloadJson = payloadJson,
            Status = JobStatus.Enabled,
            NextRunAt = nextRunAt,
            TimeoutSeconds = timeoutSeconds,
            MaxConsecutiveFailures = maxConsecutiveFailures,
            IsOneShot = isOneShot,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _scopeContext.UserId
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Scheduled job '{JobName}' ({JobId}) created with NextRunAt {NextRunAt}.", job.Name, job.Id, job.NextRunAt);

        await _eventBus.PublishAsync(new JobCreatedEvent(job.Id, job.Name, job.HandlerType, job.CronExpression), ct);

        return Result.Success(MapToDetailDto(job));
    }

    public async Task<Result<ScheduledJobDetailDto>> UpdateJobAsync(
        DefaultId id,
        string? name = null,
        string? description = null,
        string? cronExpression = null,
        string? payloadJson = null,
        int? timeoutSeconds = null,
        int? priority = null,
        int? maxConsecutiveFailures = null,
        CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<ScheduledJobDetailDto>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmedName = name.Trim();
            if (!string.Equals(job.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _dbContext.Jobs
                    .AnyAsync(j => j.Name == trimmedName && j.Id != id && j.DeletedAt == null, ct);

                if (exists)
                {
                    return Result.Failure<ScheduledJobDetailDto>(new InvalidOperationException($"A job with name '{trimmedName}' already exists."));
                }

                job.Name = trimmedName;
            }
        }

        if (description != null)
        {
            job.Description = description.Trim();
        }

        if (payloadJson != null)
        {
            job.PayloadJson = payloadJson;
        }

        if (timeoutSeconds.HasValue)
        {
            job.TimeoutSeconds = timeoutSeconds.Value;
        }

        if (priority.HasValue)
        {
            job.Priority = priority.Value;
        }

        if (maxConsecutiveFailures.HasValue)
        {
            job.MaxConsecutiveFailures = maxConsecutiveFailures.Value;
        }

        if (cronExpression != null)
        {
            var trimmedCron = cronExpression.Trim();
            if (!_cronParser.IsValid(trimmedCron))
            {
                return Result.Failure<ScheduledJobDetailDto>(new ArgumentException($"Invalid cron expression: '{cronExpression}'.", nameof(cronExpression)));
            }

            job.CronExpression = trimmedCron;
            job.IsOneShot = false;
            job.NextRunAt = _cronParser.GetNextOccurrence(trimmedCron, DateTime.UtcNow);
        }

        job.UpdatedAt = DateTime.UtcNow;
        job.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Job '{JobName}' ({JobId}) updated.", job.Name, job.Id);

        return Result.Success(MapToDetailDto(job));
    }

    public async Task<Result<bool>> DeleteJobAsync(DefaultId id, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<bool>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        job.DeletedAt = DateTime.UtcNow;
        job.DeletedBy = _scopeContext.UserId;
        job.Status = JobStatus.Disabled;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Job '{JobName}' ({JobId}) soft-deleted.", job.Name, job.Id);

        // Clean up lock if present
        await _lockService.ReleaseAsync(id, GetInstanceId(), ct);

        return Result.Success(true);
    }

    public async Task<Result<bool>> EnableJobAsync(DefaultId id, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<bool>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        job.Status = JobStatus.Enabled;
        job.ConsecutiveFailures = 0;

        if (job.NextRunAt == null || job.NextRunAt <= DateTime.UtcNow)
        {
            if (!string.IsNullOrWhiteSpace(job.CronExpression))
            {
                job.NextRunAt = _cronParser.GetNextOccurrence(job.CronExpression, DateTime.UtcNow);
            }
            else if (job.ScheduledAt.HasValue && job.ScheduledAt > DateTime.UtcNow)
            {
                job.NextRunAt = job.ScheduledAt.Value;
            }
        }

        job.UpdatedAt = DateTime.UtcNow;
        job.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Job '{JobName}' ({JobId}) enabled with NextRunAt {NextRunAt}.", job.Name, job.Id, job.NextRunAt);

        return Result.Success(true);
    }

    public async Task<Result<bool>> DisableJobAsync(DefaultId id, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<bool>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        job.Status = JobStatus.Disabled;
        job.UpdatedAt = DateTime.UtcNow;
        job.UpdatedBy = _scopeContext.UserId;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Job '{JobName}' ({JobId}) disabled.", job.Name, job.Id);

        return Result.Success(true);
    }

    public async Task<Result<JobExecutionDetailDto>> TriggerJobAsync(DefaultId id, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<JobExecutionDetailDto>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        var triggeredBy = _scopeContext.UserId ?? DefaultId.Empty;
        await _eventBus.PublishAsync(new JobTriggeredManuallyEvent(job.Id, triggeredBy), ct);

        _logger.LogInformation("Manual trigger requested for Job '{JobName}' ({JobId}) by {TriggeredBy}.", job.Name, job.Id, triggeredBy);

        return await ExecuteJobAsync(id, isManualTrigger: true, ct);
    }

    public async Task<Result<ScheduledJobDetailDto>> GetJobAsync(DefaultId id, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<ScheduledJobDetailDto>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        return Result.Success(MapToDetailDto(job));
    }

    public async Task<PaginatedResult<ScheduledJobDto>> GetJobsAsync(
        string? status = null,
        string? handlerType = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(handlerType))
        {
            query = query.Where(j => j.HandlerType.Contains(handlerType));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.NextRunAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new ScheduledJobDto(
                j.Id,
                j.Name,
                j.HandlerType,
                j.CronExpression,
                j.Status,
                j.NextRunAt,
                j.LastRunAt,
                j.LastRunStatus,
                j.CreatedAt))
            .ToListAsync(ct);

        return PaginatedResult<ScheduledJobDto>.Create(items, page, pageSize, totalCount);
    }

    public async Task<Result<IReadOnlyList<DateTime>>> GetNextOccurrencesAsync(DefaultId id, int count = 5, CancellationToken ct = default)
    {
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<IReadOnlyList<DateTime>>(new KeyNotFoundException($"Job '{id}' not found."));
        }

        if (string.IsNullOrWhiteSpace(job.CronExpression))
        {
            var single = job.ScheduledAt.HasValue && job.ScheduledAt > DateTime.UtcNow
                ? new List<DateTime> { job.ScheduledAt.Value }
                : new List<DateTime>();
            return Result.Success<IReadOnlyList<DateTime>>(single.AsReadOnly());
        }

        var occurrences = _cronParser.GetNextOccurrences(job.CronExpression, DateTime.UtcNow, count);
        return Result.Success(occurrences);
    }

    public async Task<Result<JobExecutionDetailDto>> ExecuteJobAsync(DefaultId jobId, bool isManualTrigger, CancellationToken ct = default)
    {
        var instanceId = GetInstanceId();

        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.DeletedAt == null, ct);

        if (job == null)
        {
            return Result.Failure<JobExecutionDetailDto>(new KeyNotFoundException($"Job '{jobId}' not found."));
        }

        var timeoutSeconds = job.TimeoutSeconds ?? _settings.DefaultTimeoutSeconds;
        var lockDuration = TimeSpan.FromSeconds(Math.Max(timeoutSeconds * 2, _settings.LockDurationSeconds));

        // 1. Acquire distributed lock
        var lockAcquired = await _lockService.TryAcquireAsync(job.Id, instanceId, lockDuration, ct);
        if (!lockAcquired)
        {
            _logger.LogWarning("Could not acquire lock for Job '{JobName}' ({JobId}). Execution skipped.", job.Name, job.Id);
            return Result.Failure<JobExecutionDetailDto>(new InvalidOperationException($"Job '{job.Name}' is currently locked by another instance."));
        }

        // 2. Mark job running & create execution record
        job.Status = JobStatus.Running;
        var execution = new JobExecution
        {
            Id = DefaultId.NewUlid(),
            JobId = job.Id,
            Status = ExecutionStatus.Running,
            HandlerType = job.HandlerType,
            PayloadJson = job.PayloadJson,
            IsManualTrigger = isManualTrigger,
            ExecutedByInstance = instanceId,
            StartedAt = DateTime.UtcNow
        };

        _dbContext.Executions.Add(execution);
        await _dbContext.SaveChangesAsync(ct);

        // 3. Heartbeat lock renewal background task
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Max(5, _settings.LockHeartbeatIntervalSeconds));
        var heartbeatTask = Task.Run(async () =>
        {
            while (!heartbeatCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(heartbeatInterval, heartbeatCts.Token);
                    if (!heartbeatCts.Token.IsCancellationRequested)
                    {
                        await _lockService.RenewAsync(job.Id, instanceId, lockDuration, heartbeatCts.Token);
                    }
                }
                catch
                {
                    break;
                }
            }
        });

        // 4. Resolve handler and execute
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();
        JobHandlerResult handlerResult;
        bool isTimedOut = false;

        using var scope = _scopeFactory.CreateScope();

        try
        {
            if (!_registry.TryGetHandlerType(job.HandlerType, out var handlerType) || handlerType == null)
            {
                throw new InvalidOperationException($"Handler '{job.HandlerType}' is not registered in the JobHandlerRegistry.");
            }

            var handler = (IScheduledJobHandler)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, handlerType);

            var context = new JobExecutionContext(
                JobId: job.Id,
                ExecutionId: execution.Id,
                PayloadJson: job.PayloadJson,
                Services: scope.ServiceProvider);

            handlerResult = await handler.HandleAsync(context, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            isTimedOut = true;
            handlerResult = JobHandlerResult.Failure($"Execution exceeded timeout of {timeout.TotalSeconds} seconds.");
        }
        catch (Exception ex)
        {
            handlerResult = JobHandlerResult.Failure(ex.Message);
            if (_settings.EnableStackTraceLogging)
            {
                execution.StackTrace = ex.ToString();
            }
        }
        finally
        {
            stopwatch.Stop();
            heartbeatCts.Cancel();
            try { await heartbeatTask; } catch { }
        }

        // 5. Complete execution record
        var durationMs = stopwatch.ElapsedMilliseconds;
        execution.CompletedAt = DateTime.UtcNow;
        execution.DurationMs = durationMs;
        execution.ResultJson = handlerResult.ResultJson;

        if (isTimedOut)
        {
            execution.Status = ExecutionStatus.TimedOut;
            execution.Error = handlerResult.Error;
        }
        else if (handlerResult.IsSuccess)
        {
            execution.Status = ExecutionStatus.Succeeded;
        }
        else
        {
            execution.Status = ExecutionStatus.Failed;
            execution.Error = handlerResult.Error;
        }

        // 6. Update job statistics & next schedule
        job.TotalExecutions++;
        job.LastRunAt = execution.StartedAt;
        job.LastRunStatus = execution.Status;

        if (handlerResult.IsSuccess && !isTimedOut)
        {
            job.ConsecutiveFailures = 0;
            if (job.IsOneShot)
            {
                job.Status = JobStatus.Disabled;
                job.NextRunAt = null;
            }
            else if (!string.IsNullOrWhiteSpace(job.CronExpression))
            {
                job.Status = JobStatus.Enabled;
                job.NextRunAt = _cronParser.GetNextOccurrence(job.CronExpression, DateTime.UtcNow);
            }
        }
        else
        {
            job.TotalFailures++;
            job.ConsecutiveFailures++;
            var maxFailures = job.MaxConsecutiveFailures ?? _settings.DefaultMaxConsecutiveFailures;
            if (job.ConsecutiveFailures >= maxFailures)
            {
                job.Status = JobStatus.Disabled;
                _logger.LogWarning("Job '{JobName}' ({JobId}) reached {Failures} consecutive failures. Auto-disabled.", job.Name, job.Id, job.ConsecutiveFailures);
            }
            else
            {
                job.Status = JobStatus.Enabled;
                if (!job.IsOneShot && !string.IsNullOrWhiteSpace(job.CronExpression))
                {
                    job.NextRunAt = _cronParser.GetNextOccurrence(job.CronExpression, DateTime.UtcNow);
                }
            }
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // 7. Publish domain events
        if (isTimedOut)
        {
            await _eventBus.PublishAsync(new JobTimedOutEvent(job.Id, execution.Id, job.HandlerType, (int)timeout.TotalSeconds), CancellationToken.None);
        }
        else if (handlerResult.IsSuccess)
        {
            await _eventBus.PublishAsync(new JobCompletedEvent(job.Id, execution.Id, job.HandlerType, durationMs), CancellationToken.None);
        }
        else
        {
            await _eventBus.PublishAsync(new JobFailedEvent(job.Id, execution.Id, job.HandlerType, handlerResult.Error ?? "Execution failed"), CancellationToken.None);
        }

        // 8. Release lock
        await _lockService.ReleaseAsync(job.Id, instanceId, CancellationToken.None);

        _logger.LogInformation("Job '{JobName}' ({JobId}) finished with status {Status} in {DurationMs}ms.", job.Name, job.Id, execution.Status, durationMs);

        return Result.Success(MapToExecutionDetailDto(execution));
    }

    public async Task HandleMissedFiresAsync(CancellationToken ct = default)
    {
        var policy = _settings.MissedFirePolicy.Trim();
        if (!policy.Equals("SkipToNext", StringComparison.OrdinalIgnoreCase))
        {
            // Default "FireOnce" will naturally execute due jobs during regular scheduler polling
            return;
        }

        var dueJobs = await _dbContext.Jobs
            .Where(j => j.Status == JobStatus.Enabled &&
                        j.DeletedAt == null &&
                        j.NextRunAt != null &&
                        j.NextRunAt <= DateTime.UtcNow &&
                        !string.IsNullOrEmpty(j.CronExpression))
            .ToListAsync(ct);

        foreach (var job in dueJobs)
        {
            job.NextRunAt = _cronParser.GetNextOccurrence(job.CronExpression!, DateTime.UtcNow);
        }

        if (dueJobs.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Handled {Count} missed-fire jobs using policy 'SkipToNext'.", dueJobs.Count);
        }
    }

    private static ScheduledJobDetailDto MapToDetailDto(ScheduledJob job)
    {
        return new ScheduledJobDetailDto(
            job.Id,
            job.Name,
            job.Description,
            job.HandlerType,
            job.CronExpression,
            job.ScheduledAt,
            job.PayloadJson,
            job.Status,
            job.NextRunAt,
            job.LastRunAt,
            job.LastRunStatus,
            job.TotalExecutions,
            job.TotalFailures,
            job.ConsecutiveFailures,
            job.TimeoutSeconds,
            job.Priority,
            job.IsOneShot,
            job.CreatedAt);
    }

    private static JobExecutionDetailDto MapToExecutionDetailDto(JobExecution execution)
    {
        return new JobExecutionDetailDto(
            execution.Id,
            execution.JobId,
            execution.Status,
            execution.HandlerType,
            execution.PayloadJson,
            execution.ResultJson,
            execution.Error,
            execution.StackTrace,
            execution.IsManualTrigger,
            execution.ExecutedByInstance,
            execution.StartedAt,
            execution.CompletedAt,
            execution.DurationMs);
    }
}
