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
    /// HMAC-SHA256 signing key for JWT access tokens.
    /// Must be at least 32 characters in production.
    /// </summary>
    public string JwtSecret => GetValue<string>(nameof(JwtSecret));

    /// <summary>
    /// JWT token issuer claim.
    /// </summary>
    public string JwtIssuer => GetValue<string>(nameof(JwtIssuer));

    /// <summary>
    /// JWT token audience claim.
    /// </summary>
    public string JwtAudience => GetValue<string>(nameof(JwtAudience));

    /// <summary>
    /// Access token lifetime in minutes.
    /// </summary>
    public int AccessTokenLifetimeMinutes => GetValue<int>(nameof(AccessTokenLifetimeMinutes));

    /// <summary>
    /// Refresh token lifetime in days.
    /// </summary>
    public int RefreshTokenLifetimeDays => GetValue<int>(nameof(RefreshTokenLifetimeDays));

    /// <summary>
    /// Name of the HttpOnly cookie used to store the JWT access token.
    /// </summary>
    public string CookieName => GetValue<string>(nameof(CookieName));

    /// <summary>
    /// Whether to set an HttpOnly cookie containing the JWT on login/refresh.
    /// The cookie is <c>HttpOnly + Secure + SameSite=Strict</c>.
    /// </summary>
    public bool EnableCookieAuth => GetValue<bool>(nameof(EnableCookieAuth));

    /// <summary>
    /// Email for the seeded admin user. Set to empty to skip seeding.
    /// </summary>
    public string SeedAdminEmail => GetValue<string>(nameof(SeedAdminEmail));

    /// <summary>
    /// Password for the seeded admin user.
    /// </summary>
    public string SeedAdminPassword => GetValue<string>(nameof(SeedAdminPassword));

    public IdentitySettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Identity.json", jsonSerializer, environment) { }
}
