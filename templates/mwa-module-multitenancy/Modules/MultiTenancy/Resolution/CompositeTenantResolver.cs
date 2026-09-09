using Microsoft.AspNetCore.Http;
using MultiTenancy.Configuration;

namespace MultiTenancy.Resolution;

/// <summary>
/// Chains multiple <see cref="ITenantResolver"/> strategies in priority order,
/// returning the first successfully resolved tenant ID.
/// </summary>
public class CompositeTenantResolver : ITenantResolver
{
    private readonly TenantSettings _settings;
    private readonly HeaderTenantResolver _headerResolver;
    private readonly ClaimTenantResolver _claimResolver;
    private readonly SubdomainTenantResolver _subdomainResolver;
    private readonly RouteSegmentTenantResolver _routeResolver;

    public CompositeTenantResolver(
        TenantSettings settings,
        HeaderTenantResolver headerResolver,
        ClaimTenantResolver claimResolver,
        SubdomainTenantResolver subdomainResolver,
        RouteSegmentTenantResolver routeResolver)
    {
        _settings = settings;
        _headerResolver = headerResolver;
        _claimResolver = claimResolver;
        _subdomainResolver = subdomainResolver;
        _routeResolver = routeResolver;
    }

    public async Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var order = _settings.CompositeResolverOrder;

        foreach (var strategy in order)
        {
            var resolver = GetResolver(strategy);
            if (resolver == null) continue;

            var resolved = await resolver.ResolveAsync(context, ct);
            if (resolved.HasValue && resolved.Value != default)
            {
                return resolved;
            }
        }

        return null;
    }

    private ITenantResolver? GetResolver(string strategy)
    {
        return strategy.Trim().ToLowerInvariant() switch
        {
            "header" => _headerResolver,
            "claim" => _claimResolver,
            "subdomain" => _subdomainResolver,
            "route" => _routeResolver,
            _ => null
        };
    }
}
