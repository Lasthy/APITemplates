using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// System profile with full system access across kernel and host.
/// Not public — only existing system/admins (or the startup seed process) can create system users.
/// </summary>
public class SystemProfile : IProfile
{
    public static string Name => IdentityProfiles.System;
    public static string Description => "System administrator with unrestricted access.";
    public static bool IsPublic => false;
    public static IReadOnlyList<IProfileCreationRequirement> CreationRequirements => [
        ProfileRequirements.RequireSystem()
    ];
    public static IReadOnlyList<string> Roles => [IdentityRoles.System, IdentityRoles.Admin, IdentityRoles.Employee, IdentityRoles.Buyer];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;
    public static bool CanManageApiKeys => true;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
