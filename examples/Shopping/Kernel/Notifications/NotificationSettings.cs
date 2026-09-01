using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Notifications;

/// <summary>
/// Dynamic settings for real-time notifications, loaded from <c>appsettings.Notifications.json</c>
/// with optional environment overlay <c>appsettings.Notifications.{Environment}.json</c>.
/// </summary>
public class NotificationSettings : DynamicSettingsSection
{
    private const string DefaultPath = "/hubs/notifications";

    /// <inheritdoc />
    public override string SectionName => "Notifications";

    /// <summary>
    /// Whether real-time notifications and SignalR hub endpoint are enabled.
    /// </summary>
    public bool Enabled =>
        !TryGetValue<bool>(nameof(Enabled), out var val) || val;

    /// <summary>
    /// The SignalR hub endpoint route path (e.g. "/hubs/notifications").
    /// </summary>
    public string Path =>
        TryGetValue<string>(nameof(Path), out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultPath;

    public NotificationSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Notifications.json", jsonSerializer, environment)
    {
    }

    public NotificationSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }
}
