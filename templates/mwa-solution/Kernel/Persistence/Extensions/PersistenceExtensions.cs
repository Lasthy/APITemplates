using Kernel.AuditLog;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Persistence.Extensions;

/// <summary>
/// Extension methods for registering persistence infrastructure and interceptors.
/// </summary>
public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the core EF Core SaveChanges interceptors (soft-delete, audit, structured audit log, domain events).
    /// </summary>
    public static IServiceCollection AddPersistenceInterceptors(this IServiceCollection services)
    {
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditLogSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsSaveChangesInterceptor>();
        return services;
    }
}
