using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Store manager profile.
/// Non-public — created by System only.
/// </summary>
public class ManagerProfile : IProfile
{
    public static string Name => IdentityProfiles.Manager;
    public static string Description => "Store manager profile for managing catalog products, creating employees, cancelling orders, and viewing reports.";
    public static bool IsPublic => false;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Manager];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
