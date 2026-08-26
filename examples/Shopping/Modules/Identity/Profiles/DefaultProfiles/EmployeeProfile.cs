using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// Store employee profile.
/// Non-public — created by Managers or System.
/// </summary>
public class EmployeeProfile : IProfile
{
    public static string Name => IdentityProfiles.Employee;
    public static string Description => "Store employee profile for managing inventory restock requests and fulfilling orders.";
    public static bool IsPublic => false;
    public static IReadOnlyList<string> Roles => [IdentityRoles.Employee];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
