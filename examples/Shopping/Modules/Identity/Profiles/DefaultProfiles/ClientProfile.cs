using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Public client/customer profile.
/// Assigned to users who self-register via the public registration endpoint.
/// </summary>
public class ClientProfile : IProfile
{
    public static string Name => IdentityProfiles.Client;
    public static string Description => "Customer profile for browsing products, cart management, and purchasing.";
    public static bool IsPublic => true;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Client];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
