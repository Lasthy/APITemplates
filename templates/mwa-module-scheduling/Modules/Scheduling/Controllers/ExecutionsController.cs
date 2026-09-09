using Asp.Versioning;
using Kernel.Constants;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Contracts.Dtos;
using Scheduling.Features.Executions;

namespace Scheduling.Controllers;

/// <summary>
/// Endpoints for viewing historical execution records and diagnostics.
/// Restricted to the System role.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/scheduling/executions")]
[ApiExplorerSettings(GroupName = "Scheduling")]
[Tags("Executions")]
[Authorize(Roles = Roles.System)]
public class ExecutionsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ExecutionsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// List execution history records with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<JobExecutionDto[]>> GetList(
        [FromQuery] DefaultId? jobId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetExecutions(jobId, status, from, to, page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get full execution details including result, error, and timing.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<JobExecutionDetailDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetExecution(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// List execution records for a specific scheduled job.
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    public async Task<ActionResult<JobExecutionDto[]>> GetByJobId(
        DefaultId jobId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetJobExecutions(jobId, page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
