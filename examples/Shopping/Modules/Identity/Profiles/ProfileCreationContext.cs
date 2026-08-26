using Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.Profiles;

/// <summary>
/// Context passed to <see cref="IProfile.OnUserCreatedAsync"/> during user creation.
/// Provides access to the created user, the UserManager, and the DI service provider
/// for resolving scoped services needed by the profile's creation hook.
/// </summary>
public class ProfileCreationContext
{
    /// <summary>
    /// The newly created user entity (already persisted with roles).
    /// </summary>
    public required ApplicationUser User { get; init; }

    /// <summary>
    /// The ASP.NET Identity UserManager for adding claims or modifying the user.
    /// </summary>
    public required UserManager<ApplicationUser> UserManager { get; init; }

    /// <summary>
    /// The scoped DI service provider for resolving module services.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Convenience method to add a claim to the user during creation.
    /// </summary>
    /// <param name="type">The claim type (e.g., "TenantId").</param>
    /// <param name="value">The claim value.</param>
    public async Task AddClaimAsync(string type, string value)
    {
        await UserManager.AddClaimAsync(User, new System.Security.Claims.Claim(type, value));
    }
}
