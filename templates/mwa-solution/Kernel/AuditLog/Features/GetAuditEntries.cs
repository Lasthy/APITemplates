using Kernel.Common;
using Kernel.Requests;

namespace Kernel.AuditLog.Features;

/// <summary>
/// Query request for retrieving paginated audit log entries with optional filters.
/// </summary>
public record GetAuditEntries(
    string? EntityType = null,
    string? EntityId = null,
    DefaultId? UserId = null,
    AuditAction? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    string? CorrelationId = null
) : PaginatedQuery, IRequest<PaginatedResult<AuditEntry>>;
