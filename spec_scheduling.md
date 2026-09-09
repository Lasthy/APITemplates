# Job Scheduling Module — Specification

**Template:** `mwa-module-scheduling` · **Short Name:** `modular-webapi-module-scheduling` · **Type:** Project

This module provides a lightweight, self-contained job scheduling system for MWA applications: recurring cron-based and one-shot delayed jobs, a `IScheduledJobHandler` interface for module-defined handlers, execution history, distributed locking for multi-instance safety, manual triggers, and administrative endpoints — all backed by the application's own database with no external dependencies.

---

## Module Anatomy

```
Modules/
├── Scheduling/                                       # Module implementation
│   ├── Configuration/
│   │   └── SchedulingSettings.cs                    # DynamicSettingsSection: concurrency, retention, missed-fire policy
│   ├── Controllers/
│   │   ├── JobsController.cs                        # Job CRUD, enable/disable, trigger
│   │   └── ExecutionsController.cs                  # Execution history, details
│   ├── Entities/
│   │   ├── ScheduledJob.cs                          # Job definition (name, cron, handler, payload, state)
│   │   ├── JobExecution.cs                          # Execution attempt record (status, timing, error)
│   │   └── JobLock.cs                               # Distributed lock record for multi-instance safety
│   ├── Features/
│   │   ├── Jobs/                                    # CreateJob, GetJobs, GetJob, UpdateJob, DeleteJob, EnableJob, DisableJob, TriggerJob
│   │   ├── Executions/                              # GetExecutions, GetExecution, GetJobExecutions
│   │   └── GetJobSummaryHandler.cs                  # Cross-module handler for GetJobSummary
│   ├── Handlers/
│   │   ├── IScheduledJobHandler.cs                  # Handler interface for job implementations
│   │   ├── JobHandlerRegistry.cs                    # Registry mapping handler type names → Type
│   │   └── BuiltIn/
│   │       └── NoOpJobHandler.cs                    # Default handler for testing/placeholder jobs
│   ├── Cron/
│   │   ├── ICronParser.cs                           # Cron expression parsing abstraction
│   │   └── CronParser.cs                            # 5-field cron parser (minute, hour, day, month, weekday)
│   ├── Persistence/
│   │   └── SchedulingModuleDbContext.cs             # DbContext with "scheduling" schema
│   ├── Services/
│   │   ├── IJobScheduler.cs                         # Job management operations
│   │   ├── JobScheduler.cs                          # Implementation
│   │   ├── IJobLockService.cs                       # Distributed lock acquisition/release
│   │   └── JobLockService.cs                        # Database-based pessimistic lock implementation
│   ├── Workers/
│   │   ├── JobSchedulerWorker.cs                    # Main scheduler loop: checks due jobs, acquires locks, dispatches handlers
│   │   └── ExecutionCleanupWorker.cs                # Purges old execution records past retention
│   ├── SchedulingModule.cs                          # IModule entry point
│   ├── appsettings.Scheduling.json                  # Module settings
│   └── appsettings.Scheduling.Development.json      # Dev overlay
│
└── Scheduling.Contracts/                             # Companion contracts
    ├── Constants/
    │   ├── SchedulingPolicies.cs                    # ManageJobs
    │   ├── JobStatus.cs                             # Enabled, Disabled, Running
    │   └── ExecutionStatus.cs                       # Pending, Running, Succeeded, Failed, TimedOut, Cancelled
    ├── Dtos/
    │   ├── ScheduledJobDto.cs                       # Summary DTO
    │   ├── ScheduledJobDetailDto.cs                 # Full DTO with execution stats, next run
    │   ├── JobExecutionDto.cs                       # Execution summary
    │   └── JobExecutionDetailDto.cs                 # Full execution with payload, error, timing
    ├── Events/
    │   ├── JobCompletedEvent.cs                     # (JobId, ExecutionId, HandlerType, DurationMs)
    │   ├── JobFailedEvent.cs                        # (JobId, ExecutionId, HandlerType, Error)
    │   ├── JobTimedOutEvent.cs                      # (JobId, ExecutionId, HandlerType, TimeoutSeconds)
    │   ├── JobCreatedEvent.cs                       # (JobId, Name, HandlerType, CronExpression)
    │   └── JobTriggeredManuallyEvent.cs             # (JobId, TriggeredBy)
    ├── Requests/
    │   ├── GetJobSummary.cs                         # IRequest<ScheduledJobDto>
    │   └── ScheduleJob.cs                           # IRequest<ScheduledJobDto> — programmatic job creation
    └── Scheduling.Contracts.csproj
```

---

## 1. Entities

### `ScheduledJob`

```csharp
public class ScheduledJob : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable name for this job (e.g., "Daily Report Generation").
    /// Unique within the application.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// CLR type name of the IScheduledJobHandler implementation to invoke.
    /// Resolved via JobHandlerRegistry at execution time.
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// Standard 5-field cron expression (minute hour day-of-month month day-of-week).
    /// Null for one-shot jobs that execute at ScheduledAt and don't recur.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// For one-shot jobs: the exact UTC datetime to execute.
    /// For cron jobs: null (computed from cron expression).
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Serialized JSON payload passed to the handler on execution.
    /// Schema is handler-specific.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Current status: Enabled, Disabled, Running.
    /// </summary>
    public string Status { get; set; } = "Enabled";

    /// <summary>
    /// Computed next UTC datetime this job should run.
    /// Updated after each execution (cron) or set on creation (one-shot).
    /// </summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// Last time this job was executed (regardless of outcome).
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Status of the last execution: Succeeded, Failed, TimedOut.
    /// </summary>
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// Total number of executions (lifetime).
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Total number of failed executions (lifetime).
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Maximum execution duration in seconds before the job is considered timed out.
    /// Null uses the global default from SchedulingSettings.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum number of consecutive failures before the job is auto-disabled.
    /// Null uses the global default from SchedulingSettings.
    /// </summary>
    public int? MaxConsecutiveFailures { get; set; }

    /// <summary>
    /// Current consecutive failure count. Resets on success.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Whether this is a one-shot job (executes once, then is auto-disabled).
    /// </summary>
    public bool IsOneShot { get; set; }

    /// <summary>
    /// Priority level (lower = higher priority). Used for ordering when
    /// multiple jobs are due simultaneously and MaxConcurrentJobs is limited.
    /// </summary>
    public int Priority { get; set; } = 100;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
```

### `JobExecution`

```csharp
public class JobExecution
{
    public DefaultId Id { get; set; }
    public DefaultId JobId { get; set; }

    /// <summary>
    /// Status: Pending, Running, Succeeded, Failed, TimedOut, Cancelled.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// The handler type that was invoked.
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// The payload JSON that was passed to the handler.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Optional output/result from the handler (limited to 8KB).
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Stack trace if the execution failed (Development only; redacted in Production).
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Whether this execution was triggered manually (vs. scheduled).
    /// </summary>
    public bool IsManualTrigger { get; set; }

    /// <summary>
    /// The instance/partition that executed this job (for multi-instance debugging).
    /// </summary>
    public string? ExecutedByInstance { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    // Navigation
    public ScheduledJob Job { get; set; } = null!;
}
```

### `JobLock`

```csharp
public class JobLock
{
    public DefaultId JobId { get; set; }

    /// <summary>
    /// The instance identifier that holds the lock.
    /// </summary>
    public string LockedByInstance { get; set; } = string.Empty;

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// When the lock expires (heartbeat-based; if not renewed, other instances can steal it).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
```

---

## 2. Job Handler System

### `IScheduledJobHandler`

```csharp
/// <summary>
/// Interface for scheduled job handler implementations.
/// Modules register concrete implementations that are resolved at execution time.
/// </summary>
public interface IScheduledJobHandler
{
    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <param name="context">Execution context with payload, job ID, and cancellation support.</param>
    Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct);
}

/// <summary>
/// Context provided to job handlers during execution.
/// </summary>
public record JobExecutionContext(
    DefaultId JobId,
    DefaultId ExecutionId,
    string? PayloadJson,
    IServiceProvider Services);

/// <summary>
/// Result returned by job handlers.
/// </summary>
public record JobHandlerResult(
    bool IsSuccess,
    string? ResultJson = null,
    string? Error = null)
{
    public static JobHandlerResult Success(string? resultJson = null) => new(true, resultJson);
    public static JobHandlerResult Failure(string error) => new(false, Error: error);
}
```

### How other modules define job handlers

```csharp
// In a Reporting module
public class DailyReportHandler : IScheduledJobHandler
{
    public async Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct)
    {
        var reportService = context.Services.GetRequiredService<IReportService>();
        var report = await reportService.GenerateDailyReportAsync(ct);
        return JobHandlerResult.Success(JsonSerializer.Serialize(new { ReportId = report.Id }));
    }
}
```

Then create the job via API or programmatically:
```csharp
// From another module
var result = await dispatcher.DispatchAsync(new ScheduleJob(
    Name: "Daily Sales Report",
    HandlerType: typeof(DailyReportHandler).FullName!,
    CronExpression: "0 6 * * *",   // 6 AM daily
    PayloadJson: null
), ct);
```

### `JobHandlerRegistry`

Singleton registry that maps handler type full names to CLR `Type` objects. Populated during `SchedulingModule.Register()` by scanning all loaded module assemblies for `IScheduledJobHandler` implementations. At execution time, the handler is resolved from the DI container using the registered type.

---

## 3. Cron Parser

### `ICronParser`

```csharp
public interface ICronParser
{
    /// <summary>
    /// Validates a cron expression.
    /// </summary>
    bool IsValid(string expression);

    /// <summary>
    /// Computes the next occurrence after the given UTC time.
    /// </summary>
    DateTime? GetNextOccurrence(string expression, DateTime afterUtc);

    /// <summary>
    /// Computes the next N occurrences after the given UTC time (for preview).
    /// </summary>
    IReadOnlyList<DateTime> GetNextOccurrences(string expression, DateTime afterUtc, int count);

    /// <summary>
    /// Returns a human-readable description of the cron expression.
    /// Example: "At 06:00 AM, every day"
    /// </summary>
    string Describe(string expression);
}
```

Standard 5-field format: `minute hour day-of-month month day-of-week`

Supported syntax:
- `*` (any), `,` (list), `-` (range), `/` (step)
- Weekday: `0-6` (Sunday=0) or `SUN-SAT`
- Month: `1-12` or `JAN-DEC`
- Special: `@yearly`, `@monthly`, `@weekly`, `@daily`, `@hourly`

---

## 4. Distributed Locking

### Problem

In multi-instance deployments, the `JobSchedulerWorker` runs on every instance. Without coordination, the same job could be executed simultaneously by multiple instances.

### Solution — Database Pessimistic Locking

```csharp
public interface IJobLockService
{
    /// <summary>
    /// Attempts to acquire an exclusive lock for a job.
    /// Returns true if the lock was acquired; false if another instance holds it.
    /// </summary>
    Task<bool> TryAcquireAsync(DefaultId jobId, string instanceId, TimeSpan lockDuration, CancellationToken ct);

    /// <summary>
    /// Releases the lock for a job.
    /// </summary>
    Task ReleaseAsync(DefaultId jobId, string instanceId, CancellationToken ct);

    /// <summary>
    /// Renews the lock (heartbeat) to prevent expiry during long-running jobs.
    /// </summary>
    Task<bool> RenewAsync(DefaultId jobId, string instanceId, TimeSpan extension, CancellationToken ct);
}
```

**Lock lifecycle:**
1. Worker checks if a job is due (`NextRunAt <= now` and `Status = Enabled`).
2. Worker calls `TryAcquireAsync(jobId, instanceId, lockDuration)`.
3. If acquired, sets `ScheduledJob.Status = Running`, creates a `JobExecution`, invokes the handler.
4. On completion (success or failure), releases the lock and updates `ScheduledJob.Status = Enabled`.
5. If the lock expires (handler took longer than `lockDuration` without heartbeat), another instance can steal it and mark the previous execution as `TimedOut`.

**Instance ID:** Derived from `InstanceSettings.Partition` + machine name, ensuring uniqueness across instances.

---

## 5. Persistence

### `SchedulingModuleDbContext`

```csharp
public class SchedulingModuleDbContext : DbContext, IDbContextSchema
{
    public string Schema => "scheduling";

    public DbSet<ScheduledJob> Jobs { get; set; } = null!;
    public DbSet<JobExecution> Executions { get; set; } = null!;
    public DbSet<JobLock> Locks { get; set; } = null!;
}
```

**Key indexes:**
- `ScheduledJob.Name` — unique
- `ScheduledJob.Status`
- `ScheduledJob.NextRunAt` — for worker to efficiently query due jobs
- `ScheduledJob.HandlerType`
- `JobExecution.JobId`
- `JobExecution.Status`
- `JobExecution.StartedAt`
- `JobLock.JobId` — primary key
- `JobLock.ExpiresAt`

---

## 6. Configuration

### `appsettings.Scheduling.json`

```json
{
  "PollingIntervalSeconds": 15,
  "MaxConcurrentJobs": 5,
  "DefaultTimeoutSeconds": 300,
  "DefaultMaxConsecutiveFailures": 5,
  "LockDurationSeconds": 600,
  "LockHeartbeatIntervalSeconds": 60,
  "ExecutionRetentionDays": 30,
  "CleanupIntervalHours": 24,
  "MissedFirePolicy": "FireOnce",
  "EnableStackTraceLogging": false
}
```

| Key | Type | Default | Purpose |
|---|---|---|---|
| `PollingIntervalSeconds` | `int` | `15` | How often the worker checks for due jobs |
| `MaxConcurrentJobs` | `int` | `5` | Max jobs executing simultaneously on this instance |
| `DefaultTimeoutSeconds` | `int` | `300` | Default handler timeout (5 minutes) |
| `DefaultMaxConsecutiveFailures` | `int` | `5` | Default failures before auto-disable |
| `LockDurationSeconds` | `int` | `600` | Lock TTL before expiry (10 minutes) |
| `LockHeartbeatIntervalSeconds` | `int` | `60` | How often running jobs renew their lock |
| `ExecutionRetentionDays` | `int` | `30` | Days to keep execution history |
| `CleanupIntervalHours` | `int` | `24` | Cleanup worker interval |
| `MissedFirePolicy` | `string` | `"FireOnce"` | What happens when a job's `NextRunAt` is in the past: `FireOnce` (execute once, then compute next), `SkipToNext` (just recompute next), `FireAll` (execute for each missed occurrence) |
| `EnableStackTraceLogging` | `bool` | `false` | Whether to store stack traces in `JobExecution.StackTrace` |

---

## 7. API Endpoints

### Jobs (`/api/v1/scheduling/jobs`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | System | Create a scheduled job |
| `GET` | `/` | System | List jobs (paged, filterable by status, handler type) |
| `GET` | `/{id}` | System | Get job details with execution stats and next occurrences |
| `PUT` | `/{id}` | System | Update job name, cron, payload, timeout, priority |
| `DELETE` | `/{id}` | System | Soft-delete a job |
| `POST` | `/{id}/enable` | System | Enable a disabled job |
| `POST` | `/{id}/disable` | System | Disable a job (stops future executions) |
| `POST` | `/{id}/trigger` | System | Trigger an immediate execution (bypasses cron schedule) |
| `GET` | `/{id}/next` | System | Preview next N occurrences of the cron expression |

### Executions (`/api/v1/scheduling/executions`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | System | List all executions (paged, filterable by job, status, date range) |
| `GET` | `/{id}` | System | Full execution details (payload, result, error, timing) |
| `GET` | `/jobs/{jobId}` | System | List executions for a specific job |

---

## 8. Cross-Module Contracts

### DTOs

```csharp
public record ScheduledJobDto(DefaultId Id, string Name, string HandlerType, string? CronExpression,
    string Status, DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus, DateTime CreatedAt);
public record ScheduledJobDetailDto(DefaultId Id, string Name, string? Description, string HandlerType,
    string? CronExpression, DateTime? ScheduledAt, string? PayloadJson, string Status,
    DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus,
    long TotalExecutions, long TotalFailures, int ConsecutiveFailures,
    int? TimeoutSeconds, int Priority, bool IsOneShot, DateTime CreatedAt);
public record JobExecutionDto(DefaultId Id, DefaultId JobId, string Status, string HandlerType,
    bool IsManualTrigger, DateTime StartedAt, DateTime? CompletedAt, long? DurationMs);
public record JobExecutionDetailDto(DefaultId Id, DefaultId JobId, string Status, string HandlerType,
    string? PayloadJson, string? ResultJson, string? Error, string? StackTrace,
    bool IsManualTrigger, string? ExecutedByInstance,
    DateTime StartedAt, DateTime? CompletedAt, long? DurationMs);
```

### Events

```csharp
public record JobCompletedEvent(DefaultId JobId, DefaultId ExecutionId, string HandlerType, long DurationMs) : Event;
public record JobFailedEvent(DefaultId JobId, DefaultId ExecutionId, string HandlerType, string Error) : Event;
public record JobTimedOutEvent(DefaultId JobId, DefaultId ExecutionId, string HandlerType, int TimeoutSeconds) : Event;
public record JobCreatedEvent(DefaultId JobId, string Name, string HandlerType, string? CronExpression) : Event;
public record JobTriggeredManuallyEvent(DefaultId JobId, DefaultId TriggeredBy) : Event;
```

### Cross-Module Requests

```csharp
/// <summary>
/// Query a job's summary by ID.
/// </summary>
public record GetJobSummary(DefaultId JobId) : IRequest<ScheduledJobDto>;

/// <summary>
/// Programmatically create/schedule a job from another module.
/// </summary>
public record ScheduleJob(
    string Name,
    string HandlerType,
    string? CronExpression = null,
    DateTime? ScheduledAt = null,
    string? PayloadJson = null,
    string? Description = null,
    int? TimeoutSeconds = null,
    int Priority = 100) : IRequest<ScheduledJobDto>;
```

---

## 9. Workers

### `JobSchedulerWorker`

The core scheduler. Periodic `BaseWorker` running at `PollingIntervalSeconds` interval:

1. **Query due jobs:** `SELECT * FROM ScheduledJob WHERE Status = 'Enabled' AND NextRunAt <= @now ORDER BY Priority, NextRunAt`.
2. **Respect concurrency:** Skip if `MaxConcurrentJobs` is reached (count active `JobLock` for this instance).
3. **Acquire lock:** Call `IJobLockService.TryAcquireAsync()`. Skip if another instance holds the lock.
4. **Execute:** Create `JobExecution` (status `Running`), resolve handler from DI, invoke `HandleAsync()`.
5. **Heartbeat:** For long-running jobs, a background task renews the lock at `LockHeartbeatIntervalSeconds`.
6. **Complete:** Update `JobExecution` with result, release lock, compute `NextRunAt` from cron, publish event.
7. **Missed fire:** On startup, checks for jobs where `NextRunAt` is in the past and applies `MissedFirePolicy`.

### `ExecutionCleanupWorker`

Periodic `BaseWorker` running at `CleanupIntervalHours` interval. Deletes `JobExecution` records older than `ExecutionRetentionDays`. Also cleans up expired `JobLock` records.

---

## 10. Rules & Best Practices

1. **Job handlers must be idempotent.** Due to distributed locking edge cases, a handler may execute more than once in rare scenarios.
2. **Keep handlers short.** For long-running work, have the handler enqueue the work (e.g., publish an event or create a message) and return quickly.
3. **Never reference Scheduling implementation from other modules.** Reference `Scheduling.Contracts` only. Use the `ScheduleJob` request to programmatically create jobs.
4. **Register handlers in your module's assembly.** The `JobHandlerRegistry` scans loaded assemblies during `SchedulingModule.Register()`. Handlers are resolved from DI — register any dependencies normally.
5. **Use `PayloadJson` for parameterization.** Don't hardcode behavior in handlers — pass configuration via the job's payload.
6. **Monitor `ConsecutiveFailures`.** Jobs with high failure counts indicate handler issues. The auto-disable mechanism prevents endless failing loops.
7. **One-shot jobs auto-disable.** After successful execution, one-shot jobs set `Status = Disabled`. They remain in the database for auditing.
8. **`MissedFirePolicy` matters after downtime.** If the app was down and multiple cron intervals were missed, `FireOnce` (default) executes once and catches up. `FireAll` executes for each missed interval — use with caution.
9. **Priority ordering.** Lower `Priority` values run first. Use `0` for critical jobs, `100` for normal, `999` for low-priority.
10. **All endpoints are System-only.** Job scheduling is an administrative concern. Non-System users should not have direct access.
