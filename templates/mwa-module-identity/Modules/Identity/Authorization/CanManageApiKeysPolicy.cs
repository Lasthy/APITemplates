using Identity.Configuration;
using Identity.Contracts.Constants;
using Identity.Profiles;
using Kernel.Authorization;
using Kernel.Common;
using Kernel.Context;

namespace Identity.Authorization;

/// <summary>
/// Policy governing who is permitted to manage (create, list, get, revoke) API keys.
/// <para>
/// Authorization is granted if:
/// <list type="bullet">
///   <item>The caller is an authenticated interactive user session (API keys cannot manage API keys).</item>
///   <item>API key management is enabled globally in <see cref="IdentitySettings.EnableApiKeys"/>.</item>
///   <item>The user is a System super-user (<see cref="IScopeContext.IsSystem"/>).</item>
///   <item>User creation is permitted (<see cref="IdentitySettings.AllowUserApiKeyCreation"/>) and any of:
///     <list type="bullet">
///       <item>The user has the <see cref="IdentityClaimTypes.CanManageApiKeys"/> claim set to <c>"true"</c>.</item>
///       <item>The user's profile has <see cref="IProfile.CanManageApiKeys"/> set to <c>true</c>.</item>
///       <item>The user has any role specified in <see cref="IdentitySettings.ApiKeyAllowedRoles"/>.</item>
///     </list>
///   </item>
/// </list>
/// </para>
/// </summary>
public class CanManageApiKeysPolicy : IAuthorizationPolicy
{
    public static string PolicyName => IdentityPolicies.ManageApiKeys;
    public string Name => PolicyName;

    private readonly ProfileRegistry _profileRegistry;
    private readonly IdentitySettings _identitySettings;

    public CanManageApiKeysPolicy(ProfileRegistry profileRegistry, IdentitySettings identitySettings)
    {
        _profileRegistry = profileRegistry;
        _identitySettings = identitySettings;
    }

    public Task<Result> AuthorizeAsync(
        IScopeContext context,
        object? resource,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(new UnauthorizedAccessException("Authentication required.")));
        }

        // 1. Master toggle: is API key management enabled?
        if (!_identitySettings.EnableApiKeys)
        {
            return Task.FromResult(Result.Failure(new InvalidOperationException("API key management is disabled.")));
        }

        // 2. Anti-recursion: API keys cannot manage or generate API keys
        if (context.GetClaim(IdentityClaimTypes.ApiKeyId) != null)
        {
            return Task.FromResult(Result.Failure(new UnauthorizedAccessException(
                "API keys cannot be used to manage API keys. An interactive user session is required.")));
        }

        // 3. System super-users always have unrestricted access
        if (context.IsSystem)
        {
            return Task.FromResult(Result.Success());
        }

        // 4. If settings forbid non-admin self-service creation altogether:
        if (!_identitySettings.AllowUserApiKeyCreation)
        {
            return Task.FromResult(Result.Failure(new UnauthorizedAccessException(
                "API key creation is restricted to system administrators.")));
        }

        // 5. Explicit user claim grant
        if (string.Equals(context.GetClaim(IdentityClaimTypes.CanManageApiKeys), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Success());
        }

        // 6. Profile capability check
        var profileName = context.GetClaim(IdentityClaimTypes.Profile);
        if (!string.IsNullOrEmpty(profileName) && _profileRegistry.CanManageApiKeys(profileName))
        {
            return Task.FromResult(Result.Success());
        }

        // 7. Role whitelist check
        if (_identitySettings.ApiKeyAllowedRoles.Length > 0 &&
            _identitySettings.ApiKeyAllowedRoles.Any(r => context.IsInRole(r)))
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(new UnauthorizedAccessException(
            "User is not authorized to manage API keys.")));
    }
}
