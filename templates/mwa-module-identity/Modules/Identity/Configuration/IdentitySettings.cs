using Kernel.Configuration;
using Kernel.Json;

namespace Identity.Configuration;

/// <summary>
/// Dynamic settings section for the Identity module.
/// Loaded from <c>appsettings.Identity.json</c> with optional <c>appsettings.Identity.{Environment}.json</c> overlay.
/// </summary>
public class IdentitySettings : DynamicSettingsSection
{
    public override string SectionName => "Identity";

    /// <summary>
    /// Default development placeholder for JWT secret.
    /// </summary>
    public const string DefaultJwtSecret = "CHANGE-ME-use-a-64-char-random-key-in-production-00000000000000";

    /// <summary>
    /// Default development placeholder for seed admin password.
    /// </summary>
    public const string DefaultSeedAdminPassword = "Admin123!";

    /// <summary>
    /// HMAC-SHA256 signing key for JWT access tokens.
    /// Must be at least 32 characters in production.
    /// </summary>
    [Sensitive(AllowApiMutation = false)]
    public string JwtSecret => TryGetValue<string>(nameof(JwtSecret), out var val) && !string.IsNullOrEmpty(val) ? val : DefaultJwtSecret;

    /// <summary>
    /// Whether the currently configured JwtSecret matches the default development placeholder.
    /// </summary>
    public bool IsDefaultJwtSecret => string.Equals(JwtSecret, DefaultJwtSecret, StringComparison.Ordinal);

    /// <summary>
    /// JWT token issuer claim.
    /// </summary>
    public string JwtIssuer => TryGetValue<string>(nameof(JwtIssuer), out var val) && !string.IsNullOrEmpty(val) ? val : "mwa";

    /// <summary>
    /// JWT token audience claim.
    /// </summary>
    public string JwtAudience => TryGetValue<string>(nameof(JwtAudience), out var val) && !string.IsNullOrEmpty(val) ? val : "mwa";

    /// <summary>
    /// Access token lifetime in minutes.
    /// </summary>
    public int AccessTokenLifetimeMinutes => TryGetValue<int>(nameof(AccessTokenLifetimeMinutes), out var val) && val > 0 ? val : 60;

    /// <summary>
    /// Refresh token lifetime in days.
    /// </summary>
    public int RefreshTokenLifetimeDays => TryGetValue<int>(nameof(RefreshTokenLifetimeDays), out var val) && val > 0 ? val : 7;

    /// <summary>
    /// Name of the HttpOnly cookie used to store the JWT access token.
    /// </summary>
    public string CookieName => TryGetValue<string>(nameof(CookieName), out var val) && !string.IsNullOrEmpty(val) ? val : "access_token";

    /// <summary>
    /// Whether to set an HttpOnly cookie containing the JWT on login/refresh.
    /// The cookie is <c>HttpOnly + Secure + SameSite=Lax</c>.
    /// </summary>
    public bool EnableCookieAuth => TryGetValue<bool>(nameof(EnableCookieAuth), out var val) ? val : true;

    /// <summary>
    /// Email for the seeded admin user. Set to empty to skip seeding.
    /// </summary>
    public string SeedAdminEmail => TryGetValue<string>(nameof(SeedAdminEmail), out var val) && !string.IsNullOrEmpty(val) ? val : "admin@localhost";

    /// <summary>
    /// Password for the seeded admin user.
    /// </summary>
    [Sensitive(AllowApiMutation = false)]
    public string SeedAdminPassword => TryGetValue<string>(nameof(SeedAdminPassword), out var val) && !string.IsNullOrEmpty(val) ? val : DefaultSeedAdminPassword;

    /// <summary>
    /// Whether the currently configured SeedAdminPassword matches the default development placeholder.
    /// </summary>
    public bool IsDefaultSeedAdminPassword => string.Equals(SeedAdminPassword, DefaultSeedAdminPassword, StringComparison.Ordinal);

    /// <summary>
    /// Whether API key authentication and management is enabled globally.
    /// Defaults to true.
    /// </summary>
    public bool EnableApiKeys => TryGetValue<bool>(nameof(EnableApiKeys), out var val) ? val : true;

    /// <summary>
    /// Whether non-system users with permitted profile or role can create their own API keys.
    /// If false, only System administrators can create API keys.
    /// Defaults to true.
    /// </summary>
    public bool AllowUserApiKeyCreation => TryGetValue<bool>(nameof(AllowUserApiKeyCreation), out var val) ? val : true;

    /// <summary>
    /// Additional roles permitted to create and manage API keys, in addition to System.
    /// </summary>
    public string[] ApiKeyAllowedRoles => TryGetValue<string[]>(nameof(ApiKeyAllowedRoles), out var val) && val != null ? val : [];

    /// <summary>
    /// Maximum allowed lifetime for an API key in days. Defaults to 365.
    /// If null or &lt;= 0, no maximum lifetime is enforced.
    /// </summary>
    public int? MaxApiKeyLifetimeDays => TryGetValue<int>(nameof(MaxApiKeyLifetimeDays), out var val) && val > 0 ? val : 365;

    /// <summary>
    /// Whether an expiration date is strictly required when generating an API key.
    /// Defaults to false.
    /// </summary>
    public bool RequireKeyExpiration => TryGetValue<bool>(nameof(RequireKeyExpiration), out var val) ? val : false;

    /// <summary>
    /// Maximum number of active (non-revoked, non-expired) API keys allowed per user.
    /// Defaults to 5. Set to 0 to disable limit.
    /// </summary>
    public int MaxKeysPerUser => TryGetValue<int>(nameof(MaxKeysPerUser), out var val) && val >= 0 ? val : 5;

    /// <summary>
    /// Default lifetime for dummy users in minutes. Defaults to 1440 (24 hours / 1 day).
    /// </summary>
    public int DummyUserLifetimeMinutes => TryGetValue<int>(nameof(DummyUserLifetimeMinutes), out var val) && val > 0 ? val : 1440;

    /// <summary>
    /// Whether to recycle expired dummy users when creating a new dummy user instead of accumulating records.
    /// Defaults to true.
    /// </summary>
    public bool RecycleDummyUsers => TryGetValue<bool>(nameof(RecycleDummyUsers), out var val) ? val : true;

    public IdentitySettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Identity.json", jsonSerializer, environment) { }
}
