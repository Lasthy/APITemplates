using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Default public user profile.
/// Assigned to users who self-register via the public registration endpoint.
/// Default users have no roles assigned.
/// </summary>
public class UserProfile : IProfile
{
    public static string Name => IdentityProfiles.User;
    public static string Description => "Standard user account with basic access.";
    public static bool IsPublic => true;
    public static IReadOnlyList<string> Roles => [];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
