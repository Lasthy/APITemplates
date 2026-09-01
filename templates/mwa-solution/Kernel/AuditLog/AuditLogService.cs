using Kernel.AuditLog.Features;
using Kernel.Common;
using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kernel.AuditLog;

/// <summary>
/// Default Entity Framework Core implementation of <see cref="IAuditLogService"/>.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly KernelDbContext _dbContext;

    public AuditLogService(KernelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<AuditEntry>> GetEntriesAsync(
        GetAuditEntries query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbQuery = _dbContext.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            dbQuery = dbQuery.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            dbQuery = dbQuery.Where(e => e.EntityId == query.EntityId);
        }

        if (query.UserId.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.UserId == query.UserId.Value);
        }

        if (query.Action.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Action == query.Action.Value);
        }

        if (query.From.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.OccurredAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.OccurredAt <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            dbQuery = dbQuery.Where(e => e.CorrelationId == query.CorrelationId);
        }

        if (string.IsNullOrWhiteSpace(query.SortBy))
        {
            dbQuery = dbQuery.OrderByDescending(e => e.OccurredAt);
        }

        return await dbQuery.ToPaginatedResultAsync(query, cancellationToken);
    }
}
