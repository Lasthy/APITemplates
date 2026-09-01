using Kernel.Messaging.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Messaging.Extensions;

/// <summary>
/// Extension methods for configuring and registering messaging (outbox/inbox) infrastructure.
/// </summary>
public static class MessagingExtensions
{
    /// <summary>
    /// Registers outbox/inbox messaging services, processors, and background workers in the DI container.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddScoped<IInbox, Inbox>();
        services.AddScoped<IMessageWriter, MessageWriter>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<IMessageProcessor, EventMessageProcessor>();
        services.AddScoped<IMessageProcessor, EmailMessageProcessor>();
        services.AddHostedService<MessageProcessingWorker>();
        services.AddHostedService<MessageCleanupWorker>();
        return services;
    }
}
