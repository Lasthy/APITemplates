namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known authorization policy names.
/// </summary>
public static class IdentityPolicies
{
    /// <summary>
    /// Policy requiring an authenticated user.
    /// </summary>
    public const string Authenticated = "Authenticated";

    /// <summary>
    /// Policy requiring the system super-user role.
    /// </summary>
    public const string System = "System";
}
