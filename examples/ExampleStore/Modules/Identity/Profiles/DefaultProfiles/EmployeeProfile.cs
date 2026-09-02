using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Employee profile for store staff.
/// Not public — only existing admins (or the startup seed process) can create employee users.
/// </summary>
public class EmployeeProfile : IProfile
{
    public static string Name => IdentityProfiles.Employee;
    public static string Description => "Store employee with operational and fulfillment capabilities.";
    public static bool IsPublic => false;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Employee];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;
    public static bool CanManageApiKeys => false;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
