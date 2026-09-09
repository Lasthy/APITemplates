# Scheduling — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** Job Scheduling module. It provides a lightweight, resilient job scheduling system for MWA applications: recurring cron-based and one-shot delayed jobs, the `IScheduledJobHandler` interface and registry, execution history tracking, database-backed distributed locking for multi-instance safety, manual triggers, and administrative endpoints.

---

## Module Anatomy

```
Modules/
├── Scheduling/                                       # Module implementation
│   ├── Configuration/
│   │   └── SchedulingSettings.cs                    # DynamicSettingsSection: intervals, concurrency, timeouts
│   ├── Controllers/
│   │   ├── JobsController.cs                        # Job CRUD, enable/disable, trigger, next occurrences
│   │   └── ExecutionsController.cs                  # Execution history and detail lookup
│   ├── Entities/
│   │   ├── ScheduledJob.cs                          # Job definition (name, cron, handler, payload, state)
│   │   ├── JobExecution.cs                          # Execution attempt record (status, timing, error)
│   │   └── JobLock.cs                               # Distributed lock record for multi-instance safety
│   ├── Features/
│   │   ├── Jobs/                                    # CreateJob, GetJobs, GetJob, UpdateJob, DeleteJob, EnableJob, DisableJob, TriggerJob, GetJobNextOccurrences
│   │   ├── Executions/                              # GetExecutions, GetExecution, GetJobExecutions
│   │   ├── GetJobSummaryHandler.cs                  # Cross-module handler for GetJobSummary
│   │   └── ScheduleJobHandler.cs                    # Cross-module handler for ScheduleJob
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

## Defining a Job Handler

Modules implement `IScheduledJobHandler`:

```csharp
public class InvoiceGeneratorHandler : IScheduledJobHandler
{
    public async Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct)
    {
        var invoiceService = context.Services.GetRequiredService<IInvoiceService>();
        await invoiceService.ProcessBillingCycleAsync(ct);
        return JobHandlerResult.Success("Billing cycle processed.");
    }
}
```

## Scheduling a Job

Programmatically from another module:

```csharp
var result = await dispatcher.DispatchAsync(new ScheduleJob(
    Name: "Monthly Billing Cycle",
    HandlerType: typeof(InvoiceGeneratorHandler).FullName!,
    CronExpression: "0 0 1 * *",
    PayloadJson: null
), ct);
```

Or via the System API at `POST /api/v1/scheduling/jobs`.
