using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.RateLimiting;

/// <summary>
/// Configuration for a rate limiting policy.
/// </summary>
public class RateLimitPolicyConfig
{
    /// <summary>
    /// The maximum number of permits allowed in the given window (default: 200).
    /// </summary>
    public int PermitLimit { get; set; } = 200;

    /// <summary>
    /// The time window in seconds (default: 60).
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of queued requests when permit limit is exhausted (default: 0).
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Number of segments per window for sliding window rate limiting. If 1, fixed window is used (default: 1).
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 1;
}

/// <summary>
/// Configuration for an endpoint-specific rate limit override.
/// </summary>
public class RateLimitEndpointConfig
{
    /// <summary>
    /// The maximum number of permits allowed in the given window (default: 100).
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// The time window in seconds (default: 60).
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of queued requests when permit limit is exhausted (default: 0).
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Number of segments per window for sliding window rate limiting (default: 1).
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 1;
}

/// <summary>
/// Dynamic settings for rate limiting, loaded from <c>appsettings.RateLimiting.json</c>
/// with an optional environment overlay <c>appsettings.RateLimiting.{Environment}.json</c>.
/// </summary>
public class RateLimitingSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "RateLimiting";

    /// <summary>
    /// Whether rate limiting middleware is enabled (default: true).
    /// </summary>
    public bool Enabled =>
        !TryGetValue<bool>(nameof(Enabled), out var val) || val;

    /// <summary>
    /// Default rate limiting policy applied to unauthenticated requests (per IP).
    /// </summary>
    public RateLimitPolicyConfig GlobalPolicy =>
        TryGetValue<RateLimitPolicyConfig>(nameof(GlobalPolicy), out var val) && val != null
            ? val
            : new RateLimitPolicyConfig { PermitLimit = 200, WindowSeconds = 60, QueueLimit = 0 };

    /// <summary>
    /// Default rate limiting policy applied to authenticated requests (per User ID).
    /// </summary>
    public RateLimitPolicyConfig AuthenticatedPolicy =>
        TryGetValue<RateLimitPolicyConfig>(nameof(AuthenticatedPolicy), out var val) && val != null
            ? val
            : new RateLimitPolicyConfig { PermitLimit = 500, WindowSeconds = 60, QueueLimit = 0 };

    /// <summary>
    /// Specific route/endpoint rate limit overrides (e.g. "auth/login", "auth/forgot-password").
    /// </summary>
    public Dictionary<string, RateLimitEndpointConfig> EndpointOverrides =>
        TryGetValue<Dictionary<string, RateLimitEndpointConfig>>(nameof(EndpointOverrides), out var val) && val != null
            ? val
            : new Dictionary<string, RateLimitEndpointConfig>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HTTP status code returned when rate limit is exceeded (default: 429).
    /// </summary>
    public int RejectionStatusCode =>
        TryGetValue<int>(nameof(RejectionStatusCode), out var val) && val > 0 ? val : 429;

    /// <summary>
    /// Rejection message returned in response body when rate limit is exceeded.
    /// </summary>
    public string RejectionMessage =>
        TryGetValue<string>(nameof(RejectionMessage), out var val) && !string.IsNullOrWhiteSpace(val)
            ? val
            : "Too many requests. Please try again later.";

    /// <summary>
    /// Shortcut for <c>GlobalPolicy.PermitLimit</c>.
    /// </summary>
    public int GlobalPermitLimit => GlobalPolicy.PermitLimit;

    /// <summary>
    /// Shortcut for <c>GlobalPolicy.WindowSeconds</c>.
    /// </summary>
    public int GlobalWindowSeconds => GlobalPolicy.WindowSeconds;

    /// <summary>
    /// Shortcut for <c>GlobalPolicy.QueueLimit</c>.
    /// </summary>
    public int GlobalQueueLimit => GlobalPolicy.QueueLimit;

    public RateLimitingSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.RateLimiting.json", jsonSerializer, environment)
    {
    }
}
