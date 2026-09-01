using Asp.Versioning;
using Kernel.Constants;
using Kernel.Health.Features;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kernel.Health.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/kernel/health")]
[ApiExplorerSettings(GroupName = "kernel")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IHealthService _healthService;

    public HealthController(IDispatcher dispatcher, IHealthService healthService)
    {
        _dispatcher = dispatcher;
        _healthService = healthService;
    }

    /// <summary>
    /// Liveness probe to verify that the application process is running.
    /// </summary>
    [HttpGet("live")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new { status = nameof(HealthStatus.Healthy) });
    }

    /// <summary>
    /// Readiness probe to verify that the application and its dependencies are ready to receive traffic.
    /// </summary>
    [HttpGet("ready")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var status = await _healthService.GetOverallStatusAsync(ct);

        if (status == HealthStatus.Unhealthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = status.ToString() });
        }

        return Ok(new { status = status.ToString() });
    }

    /// <summary>
    /// Detailed health report across all modules and components.
    /// Requires the <c>System</c> role.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Roles = Roles.System)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReport(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetHealthReport(), ct);

        return result.Match<IActionResult>(
            report => report.Status == HealthStatus.Unhealthy
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, report)
                : Ok(report),
            failure => BadRequest(failure.Message)
        );
    }

    /// <summary>
    /// Detailed health report for a specific module.
    /// Requires the <c>System</c> role.
    /// </summary>
    [HttpGet("report/{moduleName}")]
    [Authorize(Roles = Roles.System)]
    [ProducesResponseType(typeof(ModuleHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ModuleHealthReport), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModuleReport([FromRoute] string moduleName, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetModuleHealthReport(moduleName), ct);

        return result.Match<IActionResult>(
            report => report.Status == HealthStatus.Unhealthy
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, report)
                : Ok(report),
            failure => failure is KeyNotFoundException
                ? NotFound(failure.Message)
                : BadRequest(failure.Message)
        );
    }
}
