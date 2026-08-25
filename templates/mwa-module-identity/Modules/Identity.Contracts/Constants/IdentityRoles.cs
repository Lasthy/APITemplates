namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known role constants used by the Identity module and other modules.
/// </summary>
public static class IdentityRoles
{
    /// <summary>
    /// Standard user role with basic access.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// Administrator role with management access over users, profiles, and moderation.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// System super-user role with unrestricted administrative access across kernel and host.
    /// Aliased from <see cref="Kernel.Constants.Roles.System"/>.
    /// </summary>
    public const string System = Kernel.Constants.Roles.System;
}
