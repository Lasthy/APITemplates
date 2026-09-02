using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Administrator profile for store managers.
/// Not public — only existing admins (or the startup seed process) can create admin users.
/// </summary>
public class AdminProfile : IProfile
{
    public static string Name => IdentityProfiles.Admin;
    public static string Description => "Store administrator with full store management and moderation capabilities.";
    public static bool IsPublic => false;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Admin, IdentityRoles.Employee, IdentityRoles.Buyer];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;
    public static bool CanManageApiKeys => true;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
