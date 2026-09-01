using Kernel.Notifications;

namespace Kernel.Testing.Fakes;

/// <summary>
/// Target type for sent notifications.
/// </summary>
public enum NotificationTarget
{
    /// <summary>Notification sent to a specific user.</summary>
    User,

    /// <summary>Notification sent to a named group.</summary>
    Group,

    /// <summary>Broadcast notification sent to all connected clients.</summary>
    All
}

/// <summary>
/// Record representing a notification captured by <see cref="FakeNotificationService"/>.
/// </summary>
/// <param name="Target">The notification target type (User, Group, All).</param>
/// <param name="TargetId">The user ID string or group name, or null for broadcast.</param>
/// <param name="Channel">The notification channel or event name.</param>
/// <param name="Payload">The payload object.</param>
/// <param name="SentAt">Timestamp when the notification was sent.</param>
public record SentNotification(
    NotificationTarget Target,
    string? TargetId,
    string Channel,
    object Payload,
    DateTime SentAt)
{
    /// <summary>Parsed user ID if target is User, otherwise null.</summary>
    public DefaultId? UserId => Target == NotificationTarget.User && !string.IsNullOrEmpty(TargetId) && DefaultId.TryParse(TargetId, out var id) ? id : null;

    /// <summary>Group name if target is Group, otherwise null.</summary>
    public string? Group => Target == NotificationTarget.Group ? TargetId : null;

    /// <summary>True if this is a broadcast notification to all clients.</summary>
    public bool IsBroadcast => Target == NotificationTarget.All;
}

/// <summary>
/// In-memory fake implementation of <see cref="INotificationService"/> for unit and integration testing.
/// Captures all sent notifications and exposes assertion/query helpers.
/// </summary>
public class FakeNotificationService : INotificationService
{
    private readonly List<SentNotification> _notifications = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets all notifications sent through this service in chronological order.
    /// </summary>
    public IReadOnlyList<SentNotification> SentNotifications
    {
        get
        {
            lock (_lock)
            {
                return _notifications.ToList();
            }
        }
    }

    /// <summary>
    /// Total count of notifications sent.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _notifications.Count;
            }
        }
    }

    /// <inheritdoc />
    public Task SendToUserAsync(DefaultId userId, string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        lock (_lock)
        {
            _notifications.Add(new SentNotification(
                NotificationTarget.User,
                userId.ToString(),
                channel,
                payload,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendToGroupAsync(string group, string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        lock (_lock)
        {
            _notifications.Add(new SentNotification(
                NotificationTarget.Group,
                group,
                channel,
                payload,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendToAllAsync(string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        lock (_lock)
        {
            _notifications.Add(new SentNotification(
                NotificationTarget.All,
                null,
                channel,
                payload,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all notifications sent to the specified user.
    /// </summary>
    public IReadOnlyList<SentNotification> GetSentToUser(DefaultId userId)
    {
        lock (_lock)
        {
            return _notifications
                .Where(n => n.Target == NotificationTarget.User && string.Equals(n.TargetId, userId.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Retrieves all notifications sent to the specified group.
    /// </summary>
    public IReadOnlyList<SentNotification> GetSentToGroup(string group)
    {
        lock (_lock)
        {
            return _notifications
                .Where(n => n.Target == NotificationTarget.Group && string.Equals(n.TargetId, group, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Retrieves all broadcast notifications sent to all clients.
    /// </summary>
    public IReadOnlyList<SentNotification> GetBroadcastNotifications()
    {
        lock (_lock)
        {
            return _notifications
                .Where(n => n.Target == NotificationTarget.All)
                .ToList();
        }
    }

    /// <summary>
    /// Checks whether a notification was sent to the specified user, matching an optional channel and payload predicate.
    /// </summary>
    public bool HasSentToUser(DefaultId userId, string? channel = null, Func<object, bool>? payloadPredicate = null)
    {
        lock (_lock)
        {
            return _notifications.Any(n =>
                n.Target == NotificationTarget.User &&
                string.Equals(n.TargetId, userId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                (channel == null || string.Equals(n.Channel, channel, StringComparison.OrdinalIgnoreCase)) &&
                (payloadPredicate == null || payloadPredicate(n.Payload)));
        }
    }

    /// <summary>
    /// Checks whether a notification was sent to the specified group, matching an optional channel and payload predicate.
    /// </summary>
    public bool HasSentToGroup(string group, string? channel = null, Func<object, bool>? payloadPredicate = null)
    {
        lock (_lock)
        {
            return _notifications.Any(n =>
                n.Target == NotificationTarget.Group &&
                string.Equals(n.TargetId, group, StringComparison.OrdinalIgnoreCase) &&
                (channel == null || string.Equals(n.Channel, channel, StringComparison.OrdinalIgnoreCase)) &&
                (payloadPredicate == null || payloadPredicate(n.Payload)));
        }
    }

    /// <summary>
    /// Checks whether a broadcast notification was sent to all clients, matching an optional channel and payload predicate.
    /// </summary>
    public bool HasSentBroadcast(string? channel = null, Func<object, bool>? payloadPredicate = null)
    {
        lock (_lock)
        {
            return _notifications.Any(n =>
                n.Target == NotificationTarget.All &&
                (channel == null || string.Equals(n.Channel, channel, StringComparison.OrdinalIgnoreCase)) &&
                (payloadPredicate == null || payloadPredicate(n.Payload)));
        }
    }

    /// <summary>
    /// Clears all recorded notifications.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _notifications.Clear();
        }
    }
}
