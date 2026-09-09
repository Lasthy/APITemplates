using Microsoft.AspNetCore.Http;

namespace MultiTenancy.Resolution;

/// <summary>
/// Strategy for resolving the active tenant ID from an incoming HTTP request.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Attempts to resolve a tenant ID from the current HTTP context.
    /// Returns null if the strategy cannot resolve a tenant.
    /// </summary>
    /// <param name="context">The incoming HTTP context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved tenant ID, or null if unresolvable.</returns>
    Task<DefaultId?> ResolveAsync(HttpContext context, CancellationToken ct = default);
}
