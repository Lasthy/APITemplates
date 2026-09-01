using Kernel.Common;
using Kernel.Context;

namespace Kernel.Authorization;

/// <summary>
/// A named authorization policy with custom authorization logic.
/// Implement this interface to define policies based on database lookups,
/// resource ownership, subscription tiers, or any business rule.
/// </summary>
public interface IAuthorizationPolicy
{
    /// <summary>
    /// Unique name identifying this policy type. Referenced by [Authorize(Policy.PolicyName)] attributes or generic AuthorizeAttribute&lt;TPolicy&gt;.
    /// </summary>
    static virtual string PolicyName => string.Empty;

    /// <summary>
    /// Instance accessor returning the policy name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether the current scope context is authorized.
    /// Return Result.Success() to allow, Result.Failure(...) to deny.
    /// The request or resource object is provided for resource-level authorization.
    /// </summary>
    Task<Result> AuthorizeAsync(
        IScopeContext context,
        object? resource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Built-in policy that checks if the user is authenticated.
/// </summary>
public class AuthenticatedPolicy : IAuthorizationPolicy
{
    public static string PolicyName => "Authenticated";
    public string Name => PolicyName;

    public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(context.IsAuthenticated
            ? Result.Success()
            : Result.Failure(new UnauthorizedAccessException("Authentication required.")));
    }
}

/// <summary>
/// Built-in policy that checks if the user has any of the specified roles.
/// </summary>
public class RolePolicy : IAuthorizationPolicy
{
    private readonly string[] _roles;
    public static string PolicyName => "Role";
    public string Name { get; }

    public RolePolicy(string policyName, params string[] roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        Name = policyName;
        _roles = roles ?? [];
    }

    public Task<Result> AuthorizeAsync(IScopeContext context, object? resource, CancellationToken cancellationToken = default)
    {
        if (!context.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(new UnauthorizedAccessException("Authentication required.")));
        }

        if (_roles.Length == 0)
        {
            return Task.FromResult(Result.Success());
        }

        var hasRole = _roles.Any(r => context.IsInRole(r));
        return Task.FromResult(hasRole
            ? Result.Success()
            : Result.Failure(new UnauthorizedAccessException($"Required role: {string.Join(", ", _roles)}")));
    }
}
