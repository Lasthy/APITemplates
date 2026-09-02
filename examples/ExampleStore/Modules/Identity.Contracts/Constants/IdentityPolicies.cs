namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known authorization policy names registered by the Identity module in ExampleStore.
/// </summary>
public static class IdentityPolicies
{
    /// <summary>
    /// Policy requiring Buyer, Employee, Admin, or System access.
    /// </summary>
    public const string Buyer = "IdentityBuyer";

    /// <summary>
    /// Policy requiring Employee, Admin, or System access.
    /// </summary>
    public const string Employee = "IdentityEmployee";

    /// <summary>
    /// Policy requiring Admin or System access.
    /// </summary>
    public const string Admin = "IdentityAdmin";

    /// <summary>
    /// Policy requiring permission to manage API keys.
    /// Satisfied by System super-users, users with CanManageApiKeys profile capability,
    /// users with the CanManageApiKeys claim, or users in configured allowed roles.
    /// </summary>
    public const string ManageApiKeys = "ManageApiKeys";
}
