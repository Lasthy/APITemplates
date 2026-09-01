using Kernel.Common;
using Kernel.Requests;

namespace Kernel.AuditLog.Features;

/// <summary>
/// Handler for retrieving paginated audit log entries.
/// </summary>
[Logging]
public class GetAuditEntriesHandler : IRequestHandler<GetAuditEntries, PaginatedResult<AuditEntry>>
{
    private readonly IAuditLogService _auditLogService;

    public GetAuditEntriesHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task<Result<PaginatedResult<AuditEntry>>> HandleAsync(
        GetAuditEntries request,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditLogService.GetEntriesAsync(request, cancellationToken);
        return Result.Success(result);
    }
}
