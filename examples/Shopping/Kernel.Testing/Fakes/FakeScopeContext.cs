using Kernel.Constants;
using Kernel.Context;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="IScopeContext"/> that allows full programmatic control
/// over the current operation's user identity, roles, claims, and correlation ID in tests.
/// </summary>
public class FakeScopeContext : IScopeContext
{
    private readonly List<string> _roles = [];
    private readonly Dictionary<string, List<string>> _claims = new(StringComparer.OrdinalIgnoreCase);

    public DefaultId? UserId { get; set; }
    public bool IsAuthenticated { get; set; }
    public IReadOnlyList<string> Roles => _roles.AsReadOnly();
    public string? CorrelationId { get; set; }

    public IReadOnlyDictionary<string, string> Claims =>
        _claims.ToDictionary(k => k.Key, v => v.Value.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public bool IsSystem => IsInRole(Kernel.Constants.Roles.System);

    public string? GetClaim(string claimType)
    {
        if (_claims.TryGetValue(claimType, out var values) && values.Count > 0)
        {
            return values[0];
        }
        return null;
    }

    public IReadOnlyList<string> GetClaimValues(string claimType)
    {
        if (_claims.TryGetValue(claimType, out var values))
        {
            return values.AsReadOnly();
        }
        return Array.Empty<string>();
    }

    public bool IsInRole(string role)
    {
        return _roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Configures the context as an unauthenticated/anonymous user.
    /// </summary>
    public FakeScopeContext AsAnonymous()
    {
        IsAuthenticated = false;
        UserId = null;
        _roles.Clear();
        _claims.Clear();
        return this;
    }

    /// <summary>
    /// Configures the context as an authenticated user with the specified ID and roles.
    /// </summary>
    public FakeScopeContext AsAuthenticated(DefaultId? userId = null, params string[] roles)
    {
        IsAuthenticated = true;
        UserId = userId ?? DefaultId.NewUlid();
        _roles.Clear();
        if (roles.Length > 0)
        {
            _roles.AddRange(roles);
        }
        return this;
    }

    /// <summary>
    /// Configures the context as an authenticated super-user with the <see cref="Roles.System"/> role.
    /// </summary>
    public FakeScopeContext AsSystem(DefaultId? userId = null)
    {
        AsAuthenticated(userId, Kernel.Constants.Roles.System);
        return this;
    }

    /// <summary>
    /// Adds a role to the current context.
    /// </summary>
    public FakeScopeContext WithRole(string role)
    {
        if (!_roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
        {
            _roles.Add(role);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple roles to the current context.
    /// </summary>
    public FakeScopeContext WithRoles(params string[] roles)
    {
        foreach (var role in roles)
        {
            WithRole(role);
        }
        return this;
    }

    /// <summary>
    /// Adds a claim to the current context.
    /// </summary>
    public FakeScopeContext WithClaim(string type, string value)
    {
        if (!_claims.TryGetValue(type, out var list))
        {
            list = [];
            _claims[type] = list;
        }
        list.Add(value);
        return this;
    }

    /// <summary>
    /// Adds multiple claims to the current context.
    /// </summary>
    public FakeScopeContext WithClaims(IEnumerable<KeyValuePair<string, string>> claims)
    {
        foreach (var claim in claims)
        {
            WithClaim(claim.Key, claim.Value);
        }
        return this;
    }

    /// <summary>
    /// Sets the correlation ID for the current context.
    /// </summary>
    public FakeScopeContext WithCorrelationId(string? correlationId)
    {
        CorrelationId = correlationId;
        return this;
    }

    /// <summary>
    /// Resets the context to default unauthenticated state.
    /// </summary>
    public void Reset()
    {
        AsAnonymous();
        CorrelationId = null;
    }
}
