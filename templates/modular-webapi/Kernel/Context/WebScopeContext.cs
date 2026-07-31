using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Kernel.Context;

public class WebScopeContext
{private readonly ClaimsPrincipal? _user;
    private IReadOnlyList<string>? _roles;
    private IReadOnlyDictionary<string, string>? _claims;

    public WebScopeContext(IHttpContextAccessor httpContextAccessor)
    {
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

    public DefaultId? UserId
    {
        get
        {
            var sub = _user?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? _user?.FindFirstValue("sub");

            return sub != null ? DefaultId.Parse(sub) : (DefaultId?)null;
        }
    }

    public string? UserName =>
        _user?.FindFirstValue(ClaimTypes.Name)
        ?? _user?.FindFirstValue("name");

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        _roles ??= _user?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    public IReadOnlyDictionary<string, string> Claims =>
        _claims ??= _user?.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value)
            .AsReadOnly()
        ?? new Dictionary<string, string>().AsReadOnly();

    public string? GetClaim(string claimType) =>
        _user?.FindFirstValue(claimType);

    public IReadOnlyList<string> GetClaimValues(string claimType) =>
        _user?.FindAll(claimType)
            .Select(c => c.Value)
            .ToList()
            .AsReadOnly()
        ?? [];

    public bool IsInRole(string role) =>
        _user?.IsInRole(role) ?? false;
}
