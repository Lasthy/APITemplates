using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Contracts.Requests;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features;

/// <summary>
/// Cross-module handler for <see cref="ValidateTenantAccess"/>.
/// Allows other modules to verify whether a user has membership (and optionally a specific role) in a tenant.
/// </summary>
[Logging(Order = 1)]
public class ValidateTenantAccessHandler : IRequestHandler<ValidateTenantAccess, bool>
{
    private readonly TenantModuleDbContext _dbContext;

    public ValidateTenantAccessHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> HandleAsync(
        ValidateTenantAccess request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId && t.Status == TenantStatus.Active, cancellationToken);

        if (tenant == null)
        {
            return Result.Success(false);
        }

        var member = await _dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == request.TenantId && m.UserId == request.UserId, cancellationToken);

        if (member == null)
        {
            return Result.Success(false);
        }

        if (string.IsNullOrWhiteSpace(request.RequiredRole))
        {
            return Result.Success(true);
        }

        if (string.Equals(request.RequiredRole, member.Role, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(true);
        }

        // Owner satisfies Admin check
        if (string.Equals(request.RequiredRole, TenantRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(member.Role, TenantRoles.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(true);
        }

        return Result.Success(false);
    }
}
