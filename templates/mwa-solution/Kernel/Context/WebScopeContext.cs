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
    private IReadOnlyList<string>? _roles;
    private IReadOnlyDictionary<string, string>? _claims;

    public WebScopeContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _user = httpContextAccessor.HttpContext?.User;
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
            var sub = _user?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? _user?.FindFirstValue("sub");

            return sub != null ? DefaultId.Parse(sub) : (DefaultId?)null;
        }
    }

    /// <summary>The display name of the authenticated user.</summary>
    public string? UserName =>
        _user?.FindFirstValue(ClaimTypes.Name)
        ?? _user?.FindFirstValue("name");

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc/>
    public IReadOnlyList<string> Roles =>
        _roles ??= _user?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Claims =>
        _claims ??= _user?.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value)
            .AsReadOnly()
        ?? new Dictionary<string, string>().AsReadOnly();

    /// <inheritdoc/>
    public string? GetClaim(string claimType) =>
        _user?.FindFirstValue(claimType);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetClaimValues(string claimType) =>
        _user?.FindAll(claimType)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    /// <inheritdoc/>
    public bool IsInRole(string role) =>
        _user?.IsInRole(role) ?? false;

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
