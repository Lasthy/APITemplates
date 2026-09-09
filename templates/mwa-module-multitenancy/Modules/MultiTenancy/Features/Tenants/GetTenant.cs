using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Query to get detailed information about a single tenant organization.
/// </summary>
public record GetTenant(DefaultId Id) : IRequest<TenantDetailDto>;

/// <summary>
/// Handles <see cref="GetTenant"/> by retrieving tenant details and member count.
/// </summary>
[Logging(Order = 1)]
public class GetTenantHandler : IRequestHandler<GetTenant, TenantDetailDto>
{
    private readonly TenantModuleDbContext _dbContext;

    public GetTenantHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantDetailDto>> HandleAsync(
        GetTenant request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.Status != TenantStatus.Deleted, cancellationToken);

        if (tenant == null)
        {
            return Result.Failure<TenantDetailDto>(new KeyNotFoundException($"Tenant '{request.Id}' not found."));
        }

        var memberCount = await _dbContext.Members
            .CountAsync(m => m.TenantId == request.Id, cancellationToken);

        return Result.Success(new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.Plan,
            tenant.MetadataJson,
            tenant.OwnerUserId,
            memberCount,
            tenant.CreatedAt,
            tenant.SuspendedAt));
    }
}
