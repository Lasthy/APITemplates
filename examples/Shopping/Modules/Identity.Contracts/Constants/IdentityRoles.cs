namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known role constants used by the Identity module and other modules.
/// </summary>
public static class IdentityRoles
{
    /// <summary>
    /// Client role for customers who browse, add to cart, and buy products.
    /// </summary>
    public const string Client = "Client";

    /// <summary>
    /// Employee role for store workers who manage inventory restock and order fulfillment.
    /// </summary>
    public const string Employee = "Employee";

    /// <summary>
    /// Manager role for store managers who manage products, create employees, cancel orders, view reports.
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// System super-user role with unrestricted administrative access across kernel and host.
    /// Aliased from <see cref="Kernel.Constants.Roles.System"/>.
    /// </summary>
    public const string System = Kernel.Constants.Roles.System;

    // Backward-compatibility aliases
    public const string User = Client;
    public const string Admin = Manager;
}
