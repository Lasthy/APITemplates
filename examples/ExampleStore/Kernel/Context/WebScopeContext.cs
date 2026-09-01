using System.Security.Claims;
using Kernel.Constants;
using Kernel.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Kernel.Context;

/// <summary>
/// HTTP-context-backed implementation of <see cref="IScopeContext"/>.
/// Extracts user identity from the current <see cref="IHttpContextAccessor"/>.
/// </summary>
public class WebScopeContext : IScopeContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ClaimsPrincipal? _user;
    private readonly string? _explicitCorrelationId;

    protected ClaimsPrincipal? User => _httpContextAccessor?.HttpContext?.User ?? _user;

    public WebScopeContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Protected constructor to allow modules to create derived contexts
    /// without depending on <see cref="IHttpContextAccessor"/>.
    /// </summary>
    protected WebScopeContext(ClaimsPrincipal? user)
    {
        _user = user;
    }

    /// <summary>
    /// Protected constructor to allow modules and workers to create derived contexts
    /// with an explicit correlation identifier.
    /// </summary>
    protected WebScopeContext(ClaimsPrincipal? user, string? correlationId)
    {
        _user = user;
        _explicitCorrelationId = correlationId;
    }

    /// <inheritdoc/>
    public DefaultId? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User?.FindFirstValue("sub");

            return sub != null ? DefaultId.Parse(sub) : (DefaultId?)null;
        }
    }

    /// <summary>The display name of the authenticated user.</summary>
    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name)
        ?? User?.FindFirstValue("name");

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc/>
    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Claims =>
        User?.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value)
            .AsReadOnly()
        ?? new Dictionary<string, string>().AsReadOnly();

    /// <inheritdoc/>
    public string? GetClaim(string claimType) =>
        User?.FindFirstValue(claimType);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetClaimValues(string claimType) =>
        User?.FindAll(claimType)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    /// <inheritdoc/>
    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;

    /// <inheritdoc/>
    public bool IsSystem => IsInRole(Constants.Roles.System);

    /// <inheritdoc/>
    public string? CorrelationId
    {
        get
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                var context = _httpContextAccessor.HttpContext;
                if (context.Items.TryGetValue(DiagnosticsConstants.CorrelationIdItemKey, out var itemVal)
                    && itemVal is string s
                    && !string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }

                var headerVal = context.Request?.Headers?[DiagnosticsConstants.CorrelationIdHeader].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerVal))
                {
                    return headerVal;
                }
            }

            return _explicitCorrelationId
                ?? System.Diagnostics.Activity.Current?.GetTagItem(DiagnosticsConstants.CorrelationIdTag)?.ToString();
        }
    }
}
