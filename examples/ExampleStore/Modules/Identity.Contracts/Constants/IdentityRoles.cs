namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known role constants used by the Identity module and other modules in ExampleStore.
/// </summary>
public static class IdentityRoles
{
    /// <summary>
    /// Standard buyer/customer role with purchasing and account access.
    /// </summary>
    public const string Buyer = "Buyer";

    /// <summary>
    /// Store employee role with operational and fulfillment access.
    /// </summary>
    public const string Employee = "Employee";

    /// <summary>
    /// Administrator role with management access over users, profiles, catalog, and store settings.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// System super-user role with unrestricted administrative access across kernel and host.
    /// Aliased from <see cref="Kernel.Constants.Roles.System"/>.
    /// </summary>
    public const string System = Kernel.Constants.Roles.System;
}
