using Kernel.AuditLog;
using Kernel.Authorization.Extensions;
using Kernel.Caching.Extensions;
using Kernel.Configuration;
using Kernel.Configuration.Controllers;
using Kernel.Context;
using Kernel.Diagnostics.Extensions;
using Kernel.Email.Extensions;
using Kernel.Encryption.Extensions;
using Kernel.Events.Extensions;
using Kernel.Health;
using Kernel.Health.Extensions;
using Kernel.Json;
using Kernel.Messaging.Extensions;
using Kernel.Modules;
using Kernel.Notifications.Extensions;
using Kernel.Partitioning;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Kernel.RateLimiting.Extensions;
using Kernel.Requests.Extensions;
using Kernel.Storage.Extensions;
using Kernel.Validation.Extensions;
using Kernel.Versioning.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Extensions;

/// <summary>
/// Extension methods for wiring up the Kernel and Data infrastructure on <see cref="WebApplicationBuilder"/>
/// and <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers all Kernel services into the DI container and adds the kernel OpenAPI document.
    /// Call this before <see cref="WebApplicationBuilderExtensions.AddModules"/> in <c>Program.cs</c>.
    /// </summary>
    public static void AddKernel(this WebApplicationBuilder builder)
    {
        // ── JSON serializer ───────────────────────────────────────────────────
        var jsonSerializer = new JsonSerializer();
        builder.Services.AddSingleton<IJsonSerializer>(jsonSerializer);

        // ── HTTP context / scope context ──────────────────────────────────────
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IScopeContext, WebScopeContext>();

        // ── Authorization & Validation infrastructure ─────────────────────────
        builder.Services.AddAuthorizationPolicies(typeof(WebApplicationBuilderExtensions).Assembly);
        builder.Services.AddValidation(typeof(WebApplicationBuilderExtensions).Assembly);

        // ── Event & Module infrastructure ─────────────────────────────────────
        builder.Services.AddEvents();
        builder.Services.AddSingleton(new ModuleRegistry());

        // ── Health checks & Audit Log ─────────────────────────────────────────
        builder.Services.AddKernelHealthChecks();
        builder.Services.AddScoped<IAuditLogService, AuditLogService>();

        // ── Dynamic Settings ──────────────────────────────────────────────────
        builder.AddKernelDynamicSettings(jsonSerializer);

        // ── Feature services ──────────────────────────────────────────────────
        builder.AddKernelVersioning();
        builder.Services.AddFileStorage();
        builder.Services.AddNotifications();
        builder.AddCaching();
        builder.Services.AddEncryption();
        builder.AddRateLimiting();
        builder.Services.AddEmail();
        builder.Services.AddSingleton<IPartitionManager, PartitionManager>();
        builder.Services.AddMessaging();

        // ── Controllers & JSON Serialization ──────────────────────────────────
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Cysharp.Serialization.Json.UlidJsonConverter());
            })
            .AddApplicationPart(typeof(SettingsController).Assembly);

        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new Cysharp.Serialization.Json.UlidJsonConverter());
        });

        // ── Requests, Interceptors & Handlers ─────────────────────────────────
        builder.Services.AddRequests();

        // ── Persistence Interceptors ──────────────────────────────────────────
        builder.Services.AddPersistenceInterceptors();

        // ── OpenAPI — kernel document ─────────────────────────────────────────
        builder.Services.AddOpenApi("kernel", options =>
        {
            options.ShouldInclude = description =>
                string.Equals(description.GroupName, "kernel", StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Registers the <see cref="DataSettings"/> dynamic section and the <see cref="KernelDbContext"/>
    /// configured with the database provider specified in <c>appsettings.Data.json</c>.
    /// </summary>
    public static void AddData(this WebApplicationBuilder builder)
    {
        var dynamicSettings = builder.Services.GetRegisteredSingleton<DynamicSettings>();
        if (dynamicSettings == null)
        {
            throw new ApplicationException("DynamicSettings not found in DI. Did you call AddKernel() before AddData()?");
        }

        var jsonSerializer = builder.Services.GetRegisteredSingleton<IJsonSerializer>() ?? new JsonSerializer();
        var dataSettings = new DataSettings(jsonSerializer, dynamicSettings.Environment);
        dynamicSettings.AddSection(dataSettings);

        builder.Services.AddSingleton(dataSettings);

        builder.Services.AddDbContext<KernelDbContext>((sp, options) =>
        {
            options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "kernel");
            options.AddKernelInterceptors(sp);
        });

        // Register DbContextTypeRegistry for automatic database and table initialization
        var dbContextRegistry = new DbContextTypeRegistry();
        dbContextRegistry.Register<KernelDbContext>();
        builder.Services.AddSingleton(dbContextRegistry);

        // Register Database Health Check
        builder.Services.AddScoped<DatabaseHealthCheck>();
        var healthCheckRegistry = builder.Services.GetRegisteredSingleton<IHealthCheckRegistry>();
        healthCheckRegistry?.Register<DatabaseHealthCheck>("Data");
    }

    /// <summary>
    /// Configures the Kernel HTTP pipeline:
    /// maps the kernel OpenAPI JSON document at <c>/openapi/kernel.json</c>,
    /// maps controllers, and wires the <see cref="IEventBus"/> into
    /// <see cref="DynamicSettings"/> so that <see cref="SettingsChangedEvent"/>
    /// instances are published on value changes.
    /// </summary>
    public static void UseKernel(this WebApplication app)
    {
        // Wire the event bus into DynamicSettings now that the container is built.
        var dynamicSettings = app.Services.GetRequiredService<DynamicSettings>();
        var eventBus = app.Services.GetRequiredService<Events.IEventBus>();
        dynamicSettings.SetEventBus(eventBus);

        // HTTP correlation ID and request diagnostics
        app.UseCorrelationId();
        app.UseRequestDiagnostics();

        // Expose the OpenAPI JSON documents globally. 
        app.MapOpenApi();

        app.UseRateLimiting();

        app.MapNotifications();

        app.MapControllers();
    }

    /// <summary>
    /// Ensures that databases and tables for all registered DbContexts are automatically created on startup.
    /// Safe to call on existing databases (idempotent).
    /// </summary>
    public static void UseData(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Kernel.Data.Initialization");
        var registry = scope.ServiceProvider.GetService<DbContextTypeRegistry>();

        var contextTypes = registry?.Types ?? [typeof(KernelDbContext)];

        foreach (var contextType in contextTypes)
        {
            if (scope.ServiceProvider.GetService(contextType) is DbContext dbContext)
            {
                DatabaseInitializer.InitializeContext(dbContext, contextType, logger);
            }
        }
    }
}
