using Kernel.AuditLog.Features;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kernel.AuditLog.Controllers;

[ApiController]
[Route("api/v1/kernel/audit")]
[Authorize(Roles = Roles.System)]
[ApiExplorerSettings(GroupName = "kernel")]
[Tags("Audit")]
public class AuditController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AuditController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Retrieves a paginated list of structured audit log entries.
    /// Requires the <c>System</c> role.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AuditEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditEntries([FromQuery] GetAuditEntries request, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(request, ct);
        return result.Match<IActionResult>(
            Ok,
            failure => BadRequest(failure.Message)
        );
    }
}
