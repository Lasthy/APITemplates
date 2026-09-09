using Kernel.Authorization;
using Kernel.Common;
using Kernel.Context;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Persistence;
using MultiTenancy.Services;

namespace MultiTenancy.Authorization;

/// <summary>
/// Authorization policy requiring the caller to be the Owner of the resolved tenant,
/// or a System administrator.
/// </summary>
public class TenantOwnerPolicy : IAuthorizationPolicy
{
    public static string PolicyName => TenantPolicies.TenantOwner;
    public string Name => PolicyName;

    private readonly TenantModuleDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public TenantOwnerPolicy(TenantModuleDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result> AuthorizeAsync(
        IScopeContext context,
        object? resource,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsAuthenticated || !context.UserId.HasValue)
        {
            return Result.Failure(new UnauthorizedAccessException("Authentication required."));
        }

        if (context.IsSystem)
        {
            return Result.Success();
        }

        var tenantId = ResolveTargetTenantId(resource);
        if (!tenantId.HasValue || tenantId.Value == default)
        {
            return Result.Failure(new InvalidOperationException("Tenant ID could not be resolved for authorization."));
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value && t.Status != TenantStatus.Deleted, cancellationToken);

        if (tenant == null)
        {
            return Result.Failure(new KeyNotFoundException("Tenant not found."));
        }

        if (tenant.OwnerUserId == context.UserId.Value)
        {
            return Result.Success();
        }

        var member = await _dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId.Value && m.UserId == context.UserId.Value, cancellationToken);

        if (member?.Role == TenantRoles.Owner)
        {
            return Result.Success();
        }

        return Result.Failure(new UnauthorizedAccessException("Tenant owner role required."));
    }

    private DefaultId? ResolveTargetTenantId(object? resource)
    {
        if (resource is IDictionary<string, object?> dict)
        {
            if (dict.TryGetValue("tenantId", out var tVal) && tVal?.ToString() is { } tStr && DefaultId.TryParse(tStr, out var tid))
            {
                return tid;
            }
            if (dict.TryGetValue("id", out var idVal) && idVal?.ToString() is { } idStr && DefaultId.TryParse(idStr, out var id))
            {
                return id;
            }
        }

        return _tenantContext.TenantId;
    }
}
