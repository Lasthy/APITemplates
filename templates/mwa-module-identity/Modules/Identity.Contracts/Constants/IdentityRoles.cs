namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known role constants used by the Identity module and other modules.
/// </summary>
public static class IdentityRoles
{
    /// <summary>
    /// System super-user role with unrestricted administrative access across kernel and host.
    /// Aliased from <see cref="Kernel.Constants.Roles.System"/>.
    /// </summary>
    public const string System = Kernel.Constants.Roles.System;
}
