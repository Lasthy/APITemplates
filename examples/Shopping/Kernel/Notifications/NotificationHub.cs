using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Kernel.Notifications;

/// <summary>
/// Default SignalR Hub for real-time client notifications.
/// Supports user targeting, group subscription/unsubscription, and broadcast notifications.
/// </summary>
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub>? _logger;

    public NotificationHub()
    {
    }

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribes the caller's connection to a named notification group.
    /// </summary>
    /// <param name="group">The group name to join.</param>
    public virtual async Task JoinGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger?.LogDebug("Connection {ConnectionId} joined group {Group}", Context.ConnectionId, group);
    }

    /// <summary>
    /// Unsubscribes the caller's connection from a named notification group.
    /// </summary>
    /// <param name="group">The group name to leave.</param>
    public virtual async Task LeaveGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _logger?.LogDebug("Connection {ConnectionId} left group {Group}", Context.ConnectionId, group);
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        _logger?.LogDebug("NotificationHub: client connected {ConnectionId} (User: {UserId})",
            Context.ConnectionId, Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger?.LogDebug(exception, "NotificationHub: client disconnected {ConnectionId} (User: {UserId})",
            Context.ConnectionId, Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}
