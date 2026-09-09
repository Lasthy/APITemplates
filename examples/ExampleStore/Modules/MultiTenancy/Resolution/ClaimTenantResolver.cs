using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Persistence;

namespace MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant from claims on the authenticated user's ClaimsPrincipal.
/// </summary>
public class ClaimTenantResolver : ITenantResolver
{
    private readonly TenantModuleDbContext _dbContext;

    public ClaimTenantResolver(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // 1. Direct tenant_id claim
        var tenantIdClaim = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value
            ?? context.User.FindFirst("tenantid")?.Value
            ?? context.User.FindFirst("TenantId")?.Value;

        if (!string.IsNullOrWhiteSpace(tenantIdClaim) && DefaultId.TryParse(tenantIdClaim, out var tenantId))
        {
            return tenantId;
        }

        // 2. Tenant slug claim
        var slugClaim = context.User.FindFirst(TenantClaimTypes.TenantSlug)?.Value
            ?? context.User.FindFirst("tenant_slug")?.Value;

        if (!string.IsNullOrWhiteSpace(slugClaim))
        {
            var slug = slugClaim.Trim().ToLowerInvariant();
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug, ct);

            if (tenant != null)
            {
                return tenant.Id;
            }
        }

        return null;
    }
}
