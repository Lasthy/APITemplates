using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kernel.Notifications.Extensions;

/// <summary>
/// Extension methods for configuring real-time notifications and SignalR hub endpoints.
/// </summary>
public static class NotificationExtensions
{
    /// <summary>
    /// Registers real-time notification services, SignalR, and <see cref="KernelUserIdProvider"/> in the DI container.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSignalR();
        services.TryAddSingleton<IUserIdProvider, KernelUserIdProvider>();
        services.TryAddSingleton<INotificationService, SignalRNotificationService>();
        return services;
    }

    /// <summary>
    /// Maps the <see cref="NotificationHub"/> endpoint if notifications are enabled in <see cref="NotificationSettings"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        var settings = endpoints.ServiceProvider.GetService<NotificationSettings>();
        if (settings == null || settings.Enabled)
        {
            var path = settings?.Path ?? "/hubs/notifications";
            endpoints.MapHub<NotificationHub>(path);
        }
        return endpoints;
    }
}
