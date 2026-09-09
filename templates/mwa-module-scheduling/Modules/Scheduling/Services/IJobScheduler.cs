using Kernel.Common;
using Scheduling.Contracts.Dtos;
using Scheduling.Entities;

namespace Scheduling.Services;

/// <summary>
/// Service for managing scheduled jobs and orchestrating their execution.
/// </summary>
public interface IJobScheduler
{
    Task<Result<ScheduledJobDetailDto>> CreateJobAsync(
        string name,
        string handlerType,
        string? cronExpression = null,
        DateTime? scheduledAt = null,
        string? payloadJson = null,
        string? description = null,
        int? timeoutSeconds = null,
        int? maxConsecutiveFailures = null,
        int priority = 100,
        CancellationToken ct = default);

    Task<Result<ScheduledJobDetailDto>> UpdateJobAsync(
        DefaultId id,
        string? name = null,
        string? description = null,
        string? cronExpression = null,
        string? payloadJson = null,
        int? timeoutSeconds = null,
        int? priority = null,
        int? maxConsecutiveFailures = null,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteJobAsync(DefaultId id, CancellationToken ct = default);

    Task<Result<bool>> EnableJobAsync(DefaultId id, CancellationToken ct = default);

    Task<Result<bool>> DisableJobAsync(DefaultId id, CancellationToken ct = default);

    Task<Result<JobExecutionDetailDto>> TriggerJobAsync(DefaultId id, CancellationToken ct = default);

    Task<Result<ScheduledJobDetailDto>> GetJobAsync(DefaultId id, CancellationToken ct = default);

    Task<PaginatedResult<ScheduledJobDto>> GetJobsAsync(
        string? status = null,
        string? handlerType = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<DateTime>>> GetNextOccurrencesAsync(DefaultId id, int count = 5, CancellationToken ct = default);

    Task<Result<JobExecutionDetailDto>> ExecuteJobAsync(DefaultId jobId, bool isManualTrigger, CancellationToken ct = default);

    Task HandleMissedFiresAsync(CancellationToken ct = default);

    string GetInstanceId();
}
