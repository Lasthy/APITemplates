using Kernel.AuditLog.Features;
using Kernel.Common;

namespace Kernel.AuditLog;

/// <summary>
/// Service for querying structured audit log entries.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Retrieves a paginated list of audit log entries matching the specified query filters.
    /// </summary>
    /// <param name="query">The query filters, sorting, and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PaginatedResult{AuditEntry}"/> with matched items and pagination metadata.</returns>
    Task<PaginatedResult<AuditEntry>> GetEntriesAsync(
        GetAuditEntries query,
        CancellationToken cancellationToken = default);
}
