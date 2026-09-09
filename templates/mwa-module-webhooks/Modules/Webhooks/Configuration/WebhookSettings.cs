using Kernel.Configuration;
using Kernel.Json;

namespace Webhooks.Configuration;

/// <summary>
/// Dynamic settings section for the Webhooks module.
/// Loaded from <c>appsettings.Webhooks.json</c> with optional <c>appsettings.Webhooks.{Environment}.json</c> overlay.
/// </summary>
public class WebhookSettings : DynamicSettingsSection
{
    public override string SectionName => "Webhooks";

    /// <summary>
    /// Maximum delivery attempts before dead-lettering.
    /// Defaults to 5.
    /// </summary>
    public int MaxRetries => TryGetValue<int>(nameof(MaxRetries), out var val) && val > 0 ? val : 5;

    /// <summary>
    /// Delay for first retry in seconds.
    /// Defaults to 30.
    /// </summary>
    public int InitialRetryDelaySeconds => TryGetValue<int>(nameof(InitialRetryDelaySeconds), out var val) && val > 0 ? val : 30;

    /// <summary>
    /// Exponential backoff multiplier.
    /// Defaults to 4.0.
    /// </summary>
    public double BackoffMultiplier => TryGetValue<double>(nameof(BackoffMultiplier), out var val) && val > 0 ? val : 4.0;

    /// <summary>
    /// Cap on retry delay in minutes.
    /// Defaults to 60.
    /// </summary>
    public int MaxRetryDelayMinutes => TryGetValue<int>(nameof(MaxRetryDelayMinutes), out var val) && val > 0 ? val : 60;

    /// <summary>
    /// HTTP request timeout per delivery in seconds.
    /// Defaults to 30.
    /// </summary>
    public int DeliveryTimeoutSeconds => TryGetValue<int>(nameof(DeliveryTimeoutSeconds), out var val) && val > 0 ? val : 30;

    /// <summary>
    /// Consecutive failures before auto-disabling a subscription.
    /// Defaults to 10.
    /// </summary>
    public int MaxConsecutiveFailures => TryGetValue<int>(nameof(MaxConsecutiveFailures), out var val) && val > 0 ? val : 10;

    /// <summary>
    /// Delivery worker polling interval in seconds.
    /// Defaults to 5.
    /// </summary>
    public int ProcessingIntervalSeconds => TryGetValue<int>(nameof(ProcessingIntervalSeconds), out var val) && val > 0 ? val : 5;

    /// <summary>
    /// Days to keep delivery logs before purging.
    /// Defaults to 30.
    /// </summary>
    public int CleanupRetentionDays => TryGetValue<int>(nameof(CleanupRetentionDays), out var val) && val > 0 ? val : 30;

    /// <summary>
    /// Cleanup worker execution interval in hours.
    /// Defaults to 24.
    /// </summary>
    public int CleanupIntervalHours => TryGetValue<int>(nameof(CleanupIntervalHours), out var val) && val > 0 ? val : 24;

    /// <summary>
    /// Maximum active subscriptions allowed per user.
    /// Defaults to 20.
    /// </summary>
    public int MaxSubscriptionsPerUser => TryGetValue<int>(nameof(MaxSubscriptionsPerUser), out var val) && val > 0 ? val : 20;

    /// <summary>
    /// Maximum response body bytes stored per delivery.
    /// Defaults to 4096.
    /// </summary>
    public int MaxResponseBodyBytes => TryGetValue<int>(nameof(MaxResponseBodyBytes), out var val) && val > 0 ? val : 4096;

    /// <summary>
    /// Reject non-HTTPS subscription URLs in non-development environments.
    /// Defaults to true.
    /// </summary>
    public bool RequireHttps => TryGetValue<bool>(nameof(RequireHttps), out var val) ? val : true;

    /// <summary>
    /// Blocked URL patterns to prevent SSRF.
    /// </summary>
    public string[] BlockedUrlPatterns => TryGetValue<string[]>(nameof(BlockedUrlPatterns), out var val) && val != null
        ? val
        : ["*.internal", "localhost", "127.0.0.1", "10.*", "172.16.*", "192.168.*"];

    /// <summary>
    /// Allowed URL patterns (if non-empty, only these patterns are allowed).
    /// </summary>
    public string[] AllowedUrlPatterns => TryGetValue<string[]>(nameof(AllowedUrlPatterns), out var val) && val != null
        ? val
        : [];

    public WebhookSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Webhooks.json", jsonSerializer, environment)
    {
    }
}
