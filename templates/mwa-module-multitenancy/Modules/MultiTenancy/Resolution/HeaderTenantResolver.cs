using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Persistence;

namespace MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant from request headers (e.g. X-Tenant-Id or X-Tenant-Slug).
/// Supports both tenant ULID strings and tenant slugs.
/// </summary>
public class HeaderTenantResolver : ITenantResolver
{
    private readonly TenantSettings _settings;
    private readonly TenantModuleDbContext _dbContext;

    public HeaderTenantResolver(TenantSettings settings, TenantModuleDbContext dbContext)
    {
        _settings = settings;
        _dbContext = dbContext;
    }

    public async Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var headerName = string.IsNullOrWhiteSpace(_settings.HeaderName) ? "X-Tenant-Id" : _settings.HeaderName;

        // 1. Try configured header name or default X-Tenant-Id
        if (context.Request.Headers.TryGetValue(headerName, out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            var raw = headerVal.ToString().Trim();
            if (DefaultId.TryParse(raw, out var tenantId))
            {
                return tenantId;
            }

            // If not a valid Ulid, try resolving as slug
            var tenantBySlug = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == raw.ToLowerInvariant(), ct);

            if (tenantBySlug != null)
            {
                return tenantBySlug.Id;
            }
        }

        // 2. Try explicit X-Tenant-Slug header
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var slugVal) && !string.IsNullOrWhiteSpace(slugVal))
        {
            var slug = slugVal.ToString().Trim().ToLowerInvariant();
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
