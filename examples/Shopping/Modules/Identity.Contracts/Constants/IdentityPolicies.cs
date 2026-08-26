namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known authorization policy names registered by the Identity module.
/// </summary>
public static class IdentityPolicies
{
    public const string Client = "RequireClient";
    public const string Employee = "RequireEmployee";
    public const string Manager = "RequireManager";
    public const string System = "RequireSystem";
    public const string Admin = "RequireManager";
}
