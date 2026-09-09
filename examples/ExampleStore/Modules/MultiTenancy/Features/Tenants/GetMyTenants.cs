using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Query to list all tenants the current authenticated user belongs to.
/// </summary>
public record GetMyTenants : IRequest<IEnumerable<TenantDto>>;

/// <summary>
/// Handles <see cref="GetMyTenants"/> by querying memberships of the current user.
/// </summary>
[Logging(Order = 1)]
public class GetMyTenantsHandler : IRequestHandler<GetMyTenants, IEnumerable<TenantDto>>
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly IScopeContext _scopeContext;

    public GetMyTenantsHandler(TenantModuleDbContext dbContext, IScopeContext scopeContext)
    {
        _dbContext = dbContext;
        _scopeContext = scopeContext;
    }

    public async Task<Result<IEnumerable<TenantDto>>> HandleAsync(
        GetMyTenants request,
        CancellationToken cancellationToken = default)
    {
        if (!_scopeContext.IsAuthenticated || !_scopeContext.UserId.HasValue)
        {
            return Result.Failure<IEnumerable<TenantDto>>(new UnauthorizedAccessException("Authentication required."));
        }

        var userId = _scopeContext.UserId.Value;

        var tenants = await _dbContext.Members
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(_dbContext.Tenants.Where(t => t.Status != TenantStatus.Deleted),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new TenantDto(
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.Status.ToString(),
                    t.Plan,
                    t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<TenantDto>>(tenants);
    }
}
