using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Contracts.Requests;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features;

/// <summary>
/// Cross-module handler for <see cref="GetTenantSummary"/>.
/// Allows other modules to query basic tenant information by tenant ID.
/// </summary>
[Logging(Order = 1)]
public class GetTenantSummaryHandler : IRequestHandler<GetTenantSummary, TenantDto>
{
    private readonly TenantModuleDbContext _dbContext;

    public GetTenantSummaryHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantDto>> HandleAsync(
        GetTenantSummary request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId && t.Status != TenantStatus.Deleted, cancellationToken);

        if (tenant == null)
        {
            return Result.Failure<TenantDto>(new KeyNotFoundException($"Tenant '{request.TenantId}' not found."));
        }

        return Result.Success(new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.Plan,
            tenant.CreatedAt));
    }
}
