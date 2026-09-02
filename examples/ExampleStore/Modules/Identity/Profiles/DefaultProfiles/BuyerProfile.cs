using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Default public buyer profile.
/// Assigned to shoppers who self-register via the public registration endpoint.
/// </summary>
public class BuyerProfile : IProfile
{
    public static string Name => IdentityProfiles.Buyer;
    public static string Description => "Standard store buyer account with catalog and purchasing access.";
    public static bool IsPublic => true;
    public static IReadOnlyList<IProfileCreationRequirement> CreationRequirements => [
        ProfileRequirements.Public
    ];
    public static IReadOnlyList<string> Roles => [IdentityRoles.Buyer];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;
    public static bool CanManageApiKeys => false;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
