using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Kernel.Notifications;

/// <summary>
/// Default implementation of <see cref="INotificationService"/> backed by ASP.NET Core SignalR <see cref="IHubContext{NotificationHub}"/>.
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly NotificationSettings _settings;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hubContext,
        NotificationSettings settings,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendToUserAsync(DefaultId userId, string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_settings.Enabled)
        {
            _logger.LogDebug("Real-time notifications are disabled. Skipping notification to user {UserId} on channel {Channel}.", userId, channel);
            return;
        }

        var userIdString = userId.ToString();
        _logger.LogDebug("Sending notification to user {UserId} on channel {Channel}", userId, channel);

        await _hubContext.Clients.User(userIdString).SendAsync("ReceiveNotification", channel, payload, ct);

        if (!string.Equals(channel, "ReceiveNotification", StringComparison.OrdinalIgnoreCase))
        {
            await _hubContext.Clients.User(userIdString).SendAsync(channel, payload, ct);
        }
    }

    /// <inheritdoc />
    public async Task SendToGroupAsync(string group, string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_settings.Enabled)
        {
            _logger.LogDebug("Real-time notifications are disabled. Skipping notification to group {Group} on channel {Channel}.", group, channel);
            return;
        }

        _logger.LogDebug("Sending notification to group {Group} on channel {Channel}", group, channel);

        await _hubContext.Clients.Group(group).SendAsync("ReceiveNotification", channel, payload, ct);

        if (!string.Equals(channel, "ReceiveNotification", StringComparison.OrdinalIgnoreCase))
        {
            await _hubContext.Clients.Group(group).SendAsync(channel, payload, ct);
        }
    }

    /// <inheritdoc />
    public async Task SendToAllAsync(string channel, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_settings.Enabled)
        {
            _logger.LogDebug("Real-time notifications are disabled. Skipping broadcast notification on channel {Channel}.", channel);
            return;
        }

        _logger.LogDebug("Broadcasting notification on channel {Channel}", channel);

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", channel, payload, ct);

        if (!string.Equals(channel, "ReceiveNotification", StringComparison.OrdinalIgnoreCase))
        {
            await _hubContext.Clients.All.SendAsync(channel, payload, ct);
        }
    }
}
