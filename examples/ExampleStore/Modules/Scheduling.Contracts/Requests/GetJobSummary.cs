using Kernel.Requests;
using Scheduling.Contracts.Dtos;

namespace Scheduling.Contracts.Requests;

/// <summary>
/// Query a job's summary by ID.
/// </summary>
public record GetJobSummary(DefaultId JobId) : IRequest<ScheduledJobDto>;
