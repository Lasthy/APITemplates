using Asp.Versioning;
using Kernel.Constants;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Contracts.Dtos;
using Scheduling.Features.Jobs;

namespace Scheduling.Controllers;

/// <summary>
/// Administrative endpoints for managing scheduled jobs.
/// All endpoints are restricted to the System role.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/scheduling/jobs")]
[ApiExplorerSettings(GroupName = "Scheduling")]
[Tags("Jobs")]
[Authorize(Roles = Roles.System)]
public class JobsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public JobsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public record CreateJobRequest(
        string Name,
        string HandlerType,
        string? CronExpression = null,
        DateTime? ScheduledAt = null,
        string? PayloadJson = null,
        string? Description = null,
        int? TimeoutSeconds = null,
        int? MaxConsecutiveFailures = null,
        int Priority = 100);

    public record UpdateJobRequest(
        string? Name = null,
        string? Description = null,
        string? CronExpression = null,
        string? PayloadJson = null,
        int? TimeoutSeconds = null,
        int? Priority = null,
        int? MaxConsecutiveFailures = null);

    /// <summary>
    /// Create a new scheduled job (recurring cron or one-shot delayed).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ScheduledJobDetailDto>> Create([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new CreateJob(
            request.Name,
            request.HandlerType,
            request.CronExpression,
            request.ScheduledAt,
            request.PayloadJson,
            request.Description,
            request.TimeoutSeconds,
            request.MaxConsecutiveFailures,
            request.Priority), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// List scheduled jobs with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ScheduledJobDto[]>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] string? handlerType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetJobs(status, handlerType, page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get job details with execution statistics and next run info.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ScheduledJobDetailDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetJob(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update a job's configuration.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ScheduledJobDetailDto>> Update(DefaultId id, [FromBody] UpdateJobRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UpdateJob(
            id,
            request.Name,
            request.Description,
            request.CronExpression,
            request.PayloadJson,
            request.TimeoutSeconds,
            request.Priority,
            request.MaxConsecutiveFailures), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Soft-delete a scheduled job.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteJob(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return NoContent();
    }

    /// <summary>
    /// Enable a disabled job.
    /// </summary>
    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new EnableJob(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Disable a scheduled job to prevent further executions.
    /// </summary>
    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DisableJob(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Trigger an immediate manual execution of a scheduled job.
    /// </summary>
    [HttpPost("{id}/trigger")]
    public async Task<ActionResult<JobExecutionDetailDto>> Trigger(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new TriggerJob(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Preview the next N occurrences of a job's cron schedule.
    /// </summary>
    [HttpGet("{id}/next")]
    public async Task<ActionResult<IReadOnlyList<DateTime>>> GetNext(DefaultId id, [FromQuery] int count = 5, CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetJobNextOccurrences(id, count), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
