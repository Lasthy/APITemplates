using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Query to list all tenants (System administrators only), with pagination and status filter.
/// </summary>
public record GetTenants(TenantStatus? StatusFilter = null) : PaginatedQuery, IRequest<PaginatedResult<TenantDto>>;

/// <summary>
/// Handles <see cref="GetTenants"/> by querying the database.
/// </summary>
[Logging(Order = 1)]
public class GetTenantsHandler : IRequestHandler<GetTenants, PaginatedResult<TenantDto>>
{
    private readonly TenantModuleDbContext _dbContext;

    public GetTenantsHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<TenantDto>>> HandleAsync(
        GetTenants request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tenants.AsNoTracking().AsQueryable();

        if (request.StatusFilter.HasValue)
        {
            query = query.Where(t => t.Status == request.StatusFilter.Value);
        }
        else
        {
            query = query.Where(t => t.Status != TenantStatus.Deleted);
        }

        var projected = query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Status.ToString(),
                t.Plan,
                t.CreatedAt));

        var result = await projected.ToPaginatedResultAsync(request, cancellationToken);
        return Result.Success(result);
    }
}
