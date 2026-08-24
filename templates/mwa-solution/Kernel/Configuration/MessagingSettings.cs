using Kernel.Json;

namespace Kernel.Configuration;

/// <summary>
/// Dynamic settings for the outbox messaging subsystem, loaded from <c>settings.Messaging.json</c>
/// with an optional environment overlay <c>settings.Messaging.{Environment}.json</c>.
/// </summary>
public class MessagingSettings : DynamicSettingsSection
{
    /// <inheritdoc />
    public override string SectionName => "Messaging";

    /// <summary>
    /// Polling interval in seconds for processing pending outbox messages (default: 5).
    /// </summary>
    public int ProcessingIntervalSeconds =>
        TryGetValue<int>(nameof(ProcessingIntervalSeconds), out var val) && val > 0 ? val : 5;

    /// <summary>
    /// Maximum number of messages to fetch and process in a single batch (default: 20).
    /// </summary>
    public int ProcessingBatchSize =>
        TryGetValue<int>(nameof(ProcessingBatchSize), out var val) && val > 0 ? val : 20;

    /// <summary>
    /// Maximum duration in seconds a message can spend in the processing state before being considered stuck/timed out (default: 300).
    /// </summary>
    public int ProcessingTimeoutSeconds =>
        TryGetValue<int>(nameof(ProcessingTimeoutSeconds), out var val) && val > 0 ? val : 300;

    /// <summary>
    /// Maximum retry attempts for a failed or timed out message before marking it permanently failed (default: 5).
    /// </summary>
    public int MaxRetryAttempts =>
        TryGetValue<int>(nameof(MaxRetryAttempts), out var val) && val > 0 ? val : 5;

    /// <summary>
    /// Base delay in seconds used for exponential backoff calculations (default: 2).
    /// </summary>
    public int InitialRetryDelaySeconds =>
        TryGetValue<int>(nameof(InitialRetryDelaySeconds), out var val) && val > 0 ? val : 2;

    /// <summary>
    /// Maximum delay in seconds capped for exponential backoff (default: 3600 / 1 hour).
    /// </summary>
    public int MaxRetryDelaySeconds =>
        TryGetValue<int>(nameof(MaxRetryDelaySeconds), out var val) && val > 0 ? val : 3600;

    /// <summary>
    /// Interval in hours between runs of the message cleanup worker (default: 24).
    /// </summary>
    public int CleanupIntervalHours =>
        TryGetValue<int>(nameof(CleanupIntervalHours), out var val) && val > 0 ? val : 24;

    /// <summary>
    /// Retention period in days after which processed messages are deleted by the cleanup worker (default: 7).
    /// </summary>
    public int CleanupRetentionDays =>
        TryGetValue<int>(nameof(CleanupRetentionDays), out var val) && val > 0 ? val : 7;

    /// <summary>
    /// Whether permanently failed messages exceeding max retries should also be deleted during cleanup (default: false).
    /// </summary>
    public bool DeleteFailedMessagesOnCleanup =>
        TryGetValue<bool>(nameof(DeleteFailedMessagesOnCleanup), out var val) && val;

    public MessagingSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Messaging.json", jsonSerializer, environment)
    {
    }
}
