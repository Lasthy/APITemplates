namespace Identity.Contracts.Constants;

/// <summary>
/// Well-known claim type constants used by the Identity module.
/// </summary>
public static class IdentityClaimTypes
{
    /// <summary>
    /// Claim type for the user's profile name.
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    /// Claim type indicating whether the user is a dummy account.
    /// </summary>
    public const string IsDummy = "is_dummy";

    /// <summary>
    /// Claim type indicating when a dummy account expires.
    /// </summary>
    public const string DummyExpiresAt = "dummy_expires_at";

    /// <summary>
    /// Claim type for the ID of the API key used to authenticate.
    /// </summary>
    public const string ApiKeyId = "apikey_id";

    /// <summary>
    /// Claim type for the name/label of the API key used to authenticate.
    /// </summary>
    public const string ApiKeyName = "apikey_name";

    /// <summary>
    /// Claim type indicating whether the user is explicitly authorized to manage API keys.
    /// </summary>
    public const string CanManageApiKeys = "can_manage_api_keys";
}
