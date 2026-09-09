using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Persistence;

namespace MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant from the host's subdomain segment (e.g., "acme" from "acme.api.example.com").
/// </summary>
public class SubdomainTenantResolver : ITenantResolver
{
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www",
        "api",
        "app",
        "admin",
        "localhost",
        "mail",
        "smtp"
    };

    private readonly TenantModuleDbContext _dbContext;

    public SubdomainTenantResolver(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var parts = host.Split('.');
        // Need at least 2 parts (e.g., "tenant.localhost") or 3 parts (e.g., "tenant.example.com")
        if (parts.Length < 2)
        {
            return null;
        }

        var candidate = parts[0].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(candidate) || ReservedSubdomains.Contains(candidate))
        {
            return null;
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == candidate, ct);

        return tenant?.Id;
    }
}
