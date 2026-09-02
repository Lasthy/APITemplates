using Identity.Contracts.Constants;
using Kernel.Authorization;
using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Profiles;

/// <summary>
/// Static factory providing common built-in profile creation requirements and composition combinators.
/// </summary>
public static class ProfileRequirements
{
    /// <summary>
    /// Requirement allowing public, unauthenticated self-registration.
    /// </summary>
    public static IProfileCreationRequirement Public { get; } = new PublicProfileRequirement();

    /// <summary>
    /// Requirement allowing public, unauthenticated self-registration.
    /// </summary>
    public static IProfileCreationRequirement AllowAnonymous() => Public;

    /// <summary>
    /// Requires the caller to be authenticated.
    /// </summary>
    public static IProfileCreationRequirement RequireAuthenticated() =>
        new RequireAuthenticatedProfileRequirement();

    /// <summary>
    /// Requires the caller to have the System super-user role (<see cref="Kernel.Context.IScopeContext.IsSystem"/>).
    /// </summary>
    public static IProfileCreationRequirement RequireSystem() =>
        new RequireSystemProfileRequirement();

    /// <summary>
    /// Requires the caller to have the specified role (or be a System super-user).
    /// </summary>
    /// <param name="role">The required role name.</param>
    public static IProfileCreationRequirement RequireRole(string role) =>
        new RequireRoleProfileRequirement([role]);

    /// <summary>
    /// Requires the caller to have at least one of the specified roles (or be a System super-user).
    /// </summary>
    /// <param name="roles">The acceptable role names.</param>
    public static IProfileCreationRequirement RequireAnyRole(params string[] roles) =>
        new RequireRoleProfileRequirement(roles);

    /// <summary>
    /// Requires the caller to have all of the specified roles (or be a System super-user).
    /// </summary>
    /// <param name="roles">The required role names.</param>
    public static IProfileCreationRequirement RequireAllRoles(params string[] roles) =>
        new RequireAllRolesProfileRequirement(roles);

    /// <summary>
    /// Requires the caller to possess a claim of the specified type.
    /// </summary>
    /// <param name="claimType">The claim type required.</param>
    public static IProfileCreationRequirement RequireClaim(string claimType) =>
        new RequireClaimProfileRequirement(claimType, null);

    /// <summary>
    /// Requires the caller to possess a claim with the specified type and value.
    /// </summary>
    /// <param name="claimType">The claim type required.</param>
    /// <param name="claimValue">The expected claim value.</param>
    public static IProfileCreationRequirement RequireClaim(string claimType, string claimValue) =>
        new RequireClaimProfileRequirement(claimType, claimValue);

    /// <summary>
    /// Requires the caller's profile claim to match the specified profile name.
    /// </summary>
    /// <param name="profileName">The required profile name of the caller.</param>
    public static IProfileCreationRequirement RequireCallerProfile(string profileName) =>
        new RequireCallerProfileRequirement(profileName);

    /// <summary>
    /// Requires a Kernel <see cref="IAuthorizationPolicy"/> to succeed.
    /// </summary>
    /// <param name="policyName">The name of the policy registered in <see cref="AuthorizationPolicyRegistry"/>.</param>
    public static IProfileCreationRequirement RequirePolicy(string policyName) =>
        new PolicyProfileRequirement(policyName);

    /// <summary>
    /// Requires a strongly-typed Kernel <see cref="IAuthorizationPolicy"/> to succeed.
    /// </summary>
    /// <typeparam name="TPolicy">The policy type.</typeparam>
    public static IProfileCreationRequirement RequirePolicy<TPolicy>() where TPolicy : IAuthorizationPolicy =>
        new PolicyProfileRequirement(TPolicy.PolicyName);

    /// <summary>
    /// Evaluates a custom synchronous predicate.
    /// </summary>
    /// <param name="predicate">Function returning true to allow creation, false to deny.</param>
    /// <param name="failureMessage">Optional error message when the predicate returns false.</param>
    public static IProfileCreationRequirement Custom(
        Func<ProfileCreationRequirementContext, bool> predicate,
        string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new DelegateProfileRequirement((ctx, _) =>
        {
            var isAllowed = predicate(ctx);
            return Task.FromResult(isAllowed
                ? Result.Success()
                : Result.Failure(new UnauthorizedAccessException(failureMessage ?? "Creation requirement not satisfied.")));
        }, failureMessage ?? "Custom requirement predicate");
    }

    /// <summary>
    /// Evaluates a custom asynchronous handler.
    /// </summary>
    /// <param name="handler">Async function returning a <see cref="Result"/>.</param>
    /// <param name="description">Optional description for diagnostics.</param>
    public static IProfileCreationRequirement Custom(
        Func<ProfileCreationRequirementContext, CancellationToken, Task<Result>> handler,
        string description = "Custom asynchronous requirement")
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new DelegateProfileRequirement(handler, description);
    }

    /// <summary>
    /// Combines multiple requirements with logical OR. Satisfied if at least one requirement passes.
    /// </summary>
    /// <param name="requirements">The candidate requirements.</param>
    public static IProfileCreationRequirement Any(params IProfileCreationRequirement[] requirements) =>
        new AnyProfileRequirement(requirements);

    /// <summary>
    /// Combines multiple requirements with logical AND. Satisfied only if all requirements pass.
    /// </summary>
    /// <param name="requirements">The required requirements.</param>
    public static IProfileCreationRequirement All(params IProfileCreationRequirement[] requirements) =>
        new AllProfileRequirement(requirements);
}

/// <summary>
/// Allows unauthenticated callers to self-register.
/// </summary>
public class PublicProfileRequirement : IProfileCreationRequirement
{
    public bool IsPublic => true;
    public string Description => "Allows public anonymous self-registration.";

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default) =>
        Task.FromResult(Result.Success());
}

/// <summary>
/// Requires the caller to be authenticated.
/// </summary>
public class RequireAuthenticatedProfileRequirement : IProfileCreationRequirement
{
    public string Description => "Requires caller to be authenticated.";

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("Authentication is required to create a user with this profile.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Requires the caller to be a System super-user.
/// </summary>
public class RequireSystemProfileRequirement : IProfileCreationRequirement
{
    public string Description => "Requires System administrator role.";

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated || !context.ScopeContext.IsSystem)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("System administrator access is required to create a user with this profile.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Requires the caller to have at least one of the specified roles (or be a System super-user).
/// </summary>
public class RequireRoleProfileRequirement : IProfileCreationRequirement
{
    private readonly string[] _roles;

    public IReadOnlyList<string> Roles => _roles;
    public string Description => $"Requires any of the following roles: {string.Join(", ", _roles)}.";

    public RequireRoleProfileRequirement(IEnumerable<string> roles)
    {
        _roles = roles?.ToArray() ?? [];
    }

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("Authentication is required.")));
        }

        if (context.ScopeContext.IsSystem)
        {
            return Task.FromResult(Result.Success());
        }

        if (_roles.Length == 0)
        {
            return Task.FromResult(Result.Success());
        }

        var hasRole = _roles.Any(r => context.ScopeContext.IsInRole(r));
        if (!hasRole)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException($"Caller does not have any of the required roles: {string.Join(", ", _roles)}.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Requires the caller to have all of the specified roles (or be a System super-user).
/// </summary>
public class RequireAllRolesProfileRequirement : IProfileCreationRequirement
{
    private readonly string[] _roles;

    public IReadOnlyList<string> Roles => _roles;
    public string Description => $"Requires all of the following roles: {string.Join(", ", _roles)}.";

    public RequireAllRolesProfileRequirement(IEnumerable<string> roles)
    {
        _roles = roles?.ToArray() ?? [];
    }

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("Authentication is required.")));
        }

        if (context.ScopeContext.IsSystem)
        {
            return Task.FromResult(Result.Success());
        }

        var missingRoles = _roles.Where(r => !context.ScopeContext.IsInRole(r)).ToList();
        if (missingRoles.Count > 0)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException($"Caller is missing required roles: {string.Join(", ", missingRoles)}.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Requires the caller to possess a specific claim (and optional value).
/// </summary>
public class RequireClaimProfileRequirement : IProfileCreationRequirement
{
    private readonly string _claimType;
    private readonly string? _claimValue;

    public string ClaimType => _claimType;
    public string? ClaimValue => _claimValue;
    public string Description => _claimValue == null
        ? $"Requires claim '{_claimType}'."
        : $"Requires claim '{_claimType}' with value '{_claimValue}'.";

    public RequireClaimProfileRequirement(string claimType, string? claimValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        _claimType = claimType;
        _claimValue = claimValue;
    }

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("Authentication is required.")));
        }

        if (context.ScopeContext.IsSystem)
        {
            return Task.FromResult(Result.Success());
        }

        var actualValue = context.ScopeContext.GetClaim(_claimType);
        if (actualValue == null)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException($"Caller is missing required claim '{_claimType}'.")));
        }

        if (_claimValue != null && !string.Equals(actualValue, _claimValue, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException($"Claim '{_claimType}' expected value '{_claimValue}' but found '{actualValue}'.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Requires the caller's profile claim to match a specific profile.
/// </summary>
public class RequireCallerProfileRequirement : IProfileCreationRequirement
{
    private readonly string _profileName;

    public string ProfileName => _profileName;
    public string Description => $"Requires caller to have profile '{_profileName}'.";

    public RequireCallerProfileRequirement(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        _profileName = profileName;
    }

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (!context.ScopeContext.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException("Authentication is required.")));
        }

        if (context.ScopeContext.IsSystem)
        {
            return Task.FromResult(Result.Success());
        }

        var callerProfile = context.ScopeContext.GetClaim(IdentityClaimTypes.Profile);
        if (!string.Equals(callerProfile, _profileName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Failure(
                new UnauthorizedAccessException($"Caller profile must be '{_profileName}', but was '{callerProfile ?? "(none)"}'.")));
        }

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Delegates evaluation to a custom async function.
/// </summary>
public class DelegateProfileRequirement : IProfileCreationRequirement
{
    private readonly Func<ProfileCreationRequirementContext, CancellationToken, Task<Result>> _handler;

    public string Description { get; }

    public DelegateProfileRequirement(
        Func<ProfileCreationRequirementContext, CancellationToken, Task<Result>> handler,
        string description = "Custom delegate requirement")
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
    }

    public Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default) =>
        _handler(context, ct);
}

/// <summary>
/// Composite requirement that succeeds if ANY of its child requirements succeed (logical OR).
/// </summary>
public class AnyProfileRequirement : IProfileCreationRequirement
{
    public IReadOnlyList<IProfileCreationRequirement> Requirements { get; }
    public bool IsPublic => Requirements.Any(r => r.IsPublic);
    public string Description => $"Any of: [{string.Join(" OR ", Requirements.Select(r => r.Description))}]";

    public AnyProfileRequirement(IEnumerable<IProfileCreationRequirement> requirements)
    {
        Requirements = requirements?.ToList() ?? [];
    }

    public async Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (Requirements.Count == 0)
        {
            return Result.Success();
        }

        var failureMessages = new List<string>();
        foreach (var req in Requirements)
        {
            var result = await req.EvaluateAsync(context, ct);
            if (result.IsSuccess)
            {
                return Result.Success();
            }

            if (result.Error != null)
            {
                failureMessages.Add(result.Error.Message);
            }
        }

        return Result.Failure(new UnauthorizedAccessException(
            $"None of the requirements were satisfied: {string.Join("; ", failureMessages)}"));
    }
}

/// <summary>
/// Composite requirement that succeeds only if ALL of its child requirements succeed (logical AND).
/// </summary>
public class AllProfileRequirement : IProfileCreationRequirement
{
    public IReadOnlyList<IProfileCreationRequirement> Requirements { get; }
    public bool IsPublic => Requirements.Count > 0 && Requirements.All(r => r.IsPublic);
    public string Description => $"All of: [{string.Join(" AND ", Requirements.Select(r => r.Description))}]";

    public AllProfileRequirement(IEnumerable<IProfileCreationRequirement> requirements)
    {
        Requirements = requirements?.ToList() ?? [];
    }

    public async Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        foreach (var req in Requirements)
        {
            var result = await req.EvaluateAsync(context, ct);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}

/// <summary>
/// Evaluates a named policy registered in Kernel's <see cref="AuthorizationPolicyRegistry"/>.
/// </summary>
public class PolicyProfileRequirement : IProfileCreationRequirement
{
    private readonly string _policyName;

    public string PolicyName => _policyName;
    public string Description => $"Requires authorization policy '{_policyName}'.";

    public PolicyProfileRequirement(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        _policyName = policyName;
    }

    public async Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default)
    {
        if (context.Services == null)
        {
            return Result.Failure(new InvalidOperationException(
                $"Cannot evaluate policy '{_policyName}' because no service provider was supplied in the requirement context."));
        }

        var registry = context.Services.GetService<AuthorizationPolicyRegistry>();
        if (registry == null)
        {
            return Result.Failure(new InvalidOperationException(
                $"AuthorizationPolicyRegistry is not registered in DI container."));
        }

        var policy = registry.GetPolicy(_policyName, context.Services);
        if (policy == null)
        {
            return Result.Failure(new InvalidOperationException(
                $"Authorization policy '{_policyName}' not found in registry."));
        }

        return await policy.AuthorizeAsync(context.ScopeContext, context, ct);
    }
}
