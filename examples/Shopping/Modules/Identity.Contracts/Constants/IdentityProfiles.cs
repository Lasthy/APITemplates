namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known profile name constants.
/// </summary>
public static class IdentityProfiles
{
    /// <summary>
    /// Public client/customer profile.
    /// </summary>
    public const string Client = "Client";

    /// <summary>
    /// Store employee profile.
    /// </summary>
    public const string Employee = "Employee";

    /// <summary>
    /// Store manager profile.
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Non-public system administrator profile.
    /// </summary>
    public const string System = "System";

    // Backward-compatibility alias
    public const string User = Client;
}
