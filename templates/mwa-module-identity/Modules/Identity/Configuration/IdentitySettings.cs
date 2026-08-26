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
    public string JwtSecret => TryGetValue<string>(nameof(JwtSecret), out var val) && !string.IsNullOrEmpty(val) ? val : "CHANGE-ME-use-a-64-char-random-key-in-production-00000000000000";

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
    public string SeedAdminPassword => TryGetValue<string>(nameof(SeedAdminPassword), out var val) && !string.IsNullOrEmpty(val) ? val : "Admin123!";

    public IdentitySettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Identity.json", jsonSerializer, environment) { }
}
