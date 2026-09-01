using Kernel.AuditLog;
using Kernel.AuditLog.Features;
using Kernel.Caching;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Configuration.Features;
using Kernel.Health;
using Kernel.Health.Features;
using Kernel.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Requests.Extensions;

/// <summary>
/// Extension methods for configuring and registering request dispatching, pipeline interceptors, and core feature handlers.
/// </summary>
public static class RequestExtensions
{
    /// <summary>
    /// Registers the request dispatcher, built-in interceptors, and kernel feature request handlers in the DI container.
    /// </summary>
    public static IServiceCollection AddRequests(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<CacheInterceptor>();
        services.AddTransient<ValidationInterceptor>();
        services.AddTransient<LoggingInterceptor>();
        services.AddTransient<TransactionInterceptor>();

        // Core feature handlers
        services.AddScoped<IRequestHandler<GetSettings, string>, GetSettingsHandler>();
        services.AddScoped<IRequestHandler<UpdateSettings, bool>, UpdateSettingsHandler>();
        services.AddScoped<IRequestHandler<GetHealthReport, HealthReport>, GetHealthReportHandler>();
        services.AddScoped<IRequestHandler<GetModuleHealthReport, ModuleHealthReport>, GetModuleHealthReportHandler>();
        services.AddScoped<IRequestHandler<GetAuditEntries, PaginatedResult<AuditEntry>>, GetAuditEntriesHandler>();

        return services;
    }
}
