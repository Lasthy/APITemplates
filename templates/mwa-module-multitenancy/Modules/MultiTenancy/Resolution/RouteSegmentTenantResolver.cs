using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Persistence;

namespace MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant from route values (e.g. {tenantSlug} or {tenantId} in URL path).
/// </summary>
public class RouteSegmentTenantResolver : ITenantResolver
{
    private readonly TenantModuleDbContext _dbContext;

    public RouteSegmentTenantResolver(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var routeValues = context.GetRouteData()?.Values;
        if (routeValues == null || routeValues.Count == 0)
        {
            // Fallback: check raw path segments if route has not matched yet
            var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments != null && segments.Length > 0)
            {
                // In /api/v1/{tenant}/... segments[2] might be the tenant
                foreach (var segment in segments)
                {
                    if (DefaultId.TryParse(segment, out var candidateId))
                    {
                        var exists = await _dbContext.Tenants.AnyAsync(t => t.Id == candidateId, ct);
                        if (exists) return candidateId;
                    }
                }
            }
            return null;
        }

        // Check route parameters
        string? val = null;
        if (routeValues.TryGetValue("tenantId", out var idObj) && idObj != null)
        {
            val = idObj.ToString();
        }
        else if (routeValues.TryGetValue("tenantSlug", out var slugObj) && slugObj != null)
        {
            val = slugObj.ToString();
        }
        else if (routeValues.TryGetValue("tenant", out var tenantObj) && tenantObj != null)
        {
            val = tenantObj.ToString();
        }

        if (string.IsNullOrWhiteSpace(val))
        {
            return null;
        }

        if (DefaultId.TryParse(val, out var parsedId))
        {
            return parsedId;
        }

        var slug = val.Trim().ToLowerInvariant();
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);

        return tenant?.Id;
    }
}
