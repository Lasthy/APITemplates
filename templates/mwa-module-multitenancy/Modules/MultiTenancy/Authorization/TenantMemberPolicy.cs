using Kernel.Authorization;
using Kernel.Common;
using Kernel.Context;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Persistence;
using MultiTenancy.Services;

namespace MultiTenancy.Authorization;

/// <summary>
/// Authorization policy requiring the caller to be an active member of the resolved tenant,
/// or a System administrator.
/// </summary>
public class TenantMemberPolicy : IAuthorizationPolicy
{
    public static string PolicyName => TenantPolicies.TenantMember;
    public string Name => PolicyName;

    private readonly TenantModuleDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public TenantMemberPolicy(TenantModuleDbContext dbContext, ITenantContext tenantContext)
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

        var isMember = await _dbContext.Members
            .AnyAsync(m => m.TenantId == tenantId.Value && m.UserId == context.UserId.Value, cancellationToken);

        if (!isMember)
        {
            return Result.Failure(new UnauthorizedAccessException("Tenant membership required."));
        }

        return Result.Success();
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
