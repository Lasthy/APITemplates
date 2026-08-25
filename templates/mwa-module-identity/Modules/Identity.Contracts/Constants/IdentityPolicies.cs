namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known authorization policy names registered by the Identity module.
/// </summary>
public static class IdentityPolicies
{
    /// <summary>
    /// Policy requiring the <see cref="IdentityRoles.Admin"/> role.
    /// </summary>
    public const string Admin = "IdentityAdmin";
}
