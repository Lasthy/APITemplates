using Identity.Contracts.Constants;

namespace Identity.Profiles.DefaultProfiles;

/// <summary>
/// System profile with full system access.
/// Not public — only existing system administrators (or the startup seed process) can create system users.
/// </summary>
public class SystemProfile : IProfile
{
    public static string Name => IdentityProfiles.System;
    public static string Description => "System administrator with full access.";
    public static bool IsPublic => false;
    public static IReadOnlyList<string> Roles => [IdentityRoles.System];
    public static IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
    public static Type? PreferencesType => null;

    public static Task OnUserCreatedAsync(ProfileCreationContext context, CancellationToken ct)
        => Task.CompletedTask;
}
