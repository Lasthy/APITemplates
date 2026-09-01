namespace Kernel.Notifications;

/// <summary>
/// Abstraction for sending real-time notifications to connected clients.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a real-time notification payload to a specific user across all their active connections.
    /// </summary>
    /// <param name="userId">The target user's identifier.</param>
    /// <param name="channel">The notification channel or event name (e.g. "OrderUpdated", "notification").</param>
    /// <param name="payload">The notification payload object.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendToUserAsync(DefaultId userId, string channel, object payload, CancellationToken ct = default);

    /// <summary>
    /// Sends a real-time notification payload to all connected clients in a specific group.
    /// </summary>
    /// <param name="group">The group name (e.g. "Admins", "Tenant_123").</param>
    /// <param name="channel">The notification channel or event name.</param>
    /// <param name="payload">The notification payload object.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendToGroupAsync(string group, string channel, object payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a real-time notification payload to all connected clients.
    /// </summary>
    /// <param name="channel">The notification channel or event name.</param>
    /// <param name="payload">The notification payload object.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendToAllAsync(string channel, object payload, CancellationToken ct = default);
}
