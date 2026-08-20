using Kernel.Boxing;
using Kernel.Configuration;
using Kernel.Configuration.Endpoints;
using Kernel.Context;
using Kernel.Events;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Modules;
using Kernel.Partitioning;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Kernel.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Extensions;

/// <summary>
/// Extension methods for wiring up the Kernel layer on <see cref="WebApplicationBuilder"/>
/// and <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers all Kernel services into the DI container and adds the kernel OpenAPI document.
    /// Call this before <see cref="WebApplicationBuilderExtension.AddModules"/> in <c>Program.cs</c>.
    /// </summary>
    /// <remarks>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><see cref="IJsonSerializer"/> — singleton, backed by <see cref="JsonSerializer"/>.</item>
    ///   <item><see cref="IHttpContextAccessor"/> — singleton (ASP.NET built-in).</item>
    ///   <item><see cref="IScopeContext"/> — scoped, backed by <see cref="WebScopeContext"/>.</item>
    ///   <item><see cref="IEventBus"/> — singleton, backed by <see cref="InProcessEventBus"/>.</item>
    ///   <item><see cref="IEventTypeRegistry"/> — singleton, backed by <see cref="EventTypeRegistry"/>.</item>
    ///   <item><see cref="InstanceSettings"/> — singleton, loaded from <c>appsettings.Instance.json</c>.</item>
    ///   <item><see cref="IPartitionManager"/> — singleton, backed by <see cref="PartitionManager"/>.</item>
    ///   <item><see cref="IMessageWriter"/> — scoped, backed by <see cref="MessageWriter"/>.</item>
    ///   <item><see cref="IMessageDispatcher"/> — scoped, backed by <see cref="MessageDispatcher"/>.</item>
    ///   <item><see cref="IMessageProcessor"/> (Event channel) — scoped, backed by <see cref="EventMessageProcessor"/>.</item>
    ///   <item><see cref="ApiSettings"/> — singleton bound from <c>appsettings.json#Api</c>.</item>
    ///   <item>OpenAPI document named <c>"kernel"</c>.</item>
    /// </list>
    /// </remarks>
    public static void AddKernel(this WebApplicationBuilder builder)
    {
        // ── JSON serializer ───────────────────────────────────────────────────
        builder.Services.AddSingleton<IJsonSerializer, JsonSerializer>();

        // ── HTTP context / scope context ──────────────────────────────────────
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IScopeContext, WebScopeContext>();

        // ── Event infrastructure ──────────────────────────────────────────────
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
        builder.Services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
        builder.Services.AddSingleton(new ModuleRegistry());

        // ── Dynamic Settings ──────────────────────────────────────────────────
        var dynamicSettings = new DynamicSettings(new JsonSerializer());
        builder.Services.AddSingleton(dynamicSettings);

        builder.Services.AddSingleton<AppSettings>(sp =>
        {
            var ds = sp.GetRequiredService<DynamicSettings>();
            var json = sp.GetRequiredService<IJsonSerializer>();
            // Only add if not already added (e.g. by another initialization step)
            try { return ds.GetSection<AppSettings>(); }
            catch { return ds.AddSection(new AppSettings("appsettings.json", json)); }
        });

        builder.Services.AddSingleton<InstanceSettings>(sp =>
        {
            var ds = sp.GetRequiredService<DynamicSettings>();
            var json = sp.GetRequiredService<IJsonSerializer>();
            try { return ds.GetSection<InstanceSettings>(); }
            catch { return ds.AddSection(new InstanceSettings("appsettings.Instance.json", json)); }
        });

        // ── Partition manager ─────────────────────────────────────────────────
        builder.Services.AddSingleton<IPartitionManager, PartitionManager>();

        // ── Messaging / outbox ────────────────────────────────────────────────
        builder.Services.AddScoped<IMessageWriter, MessageWriter>();
        builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        builder.Services.AddScoped<IMessageProcessor, EventMessageProcessor>();

        // ── Configuration ─────────────────────────────────────────────────────
        var apiSettings = builder.Configuration
            .GetSection(ApiSettings.SectionName)
            .Get<ApiSettings>() ?? new ApiSettings();

        builder.Services.AddSingleton(apiSettings);

        // ── Configuration Endpoints Requests ──────────────────────────────────
        builder.Services.AddScoped<IDispatcher, Dispatcher>();
        builder.Services.AddScoped<IRequestHandler<Kernel.Configuration.Requests.GetSettingsRequest, string>, Kernel.Configuration.Requests.GetSettingsRequestHandler>();
        builder.Services.AddScoped<IRequestHandler<Kernel.Configuration.Requests.UpdateSettingsRequest, bool>, Kernel.Configuration.Requests.UpdateSettingsRequestHandler>();

        // ── Database / Persistence ────────────────────────────────────────────
        var databaseOptions = builder.Configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();
        
        builder.Services.AddSingleton(databaseOptions);
        
        builder.Services.AddDbContext<KernelDbContext>(options =>
        {
            options.UseConfiguredDatabase(databaseOptions.Provider, databaseOptions.ConnectionString);
        });

        // ── OpenAPI — kernel document ─────────────────────────────────────────
        builder.Services.AddOpenApi("kernel");
    }

    /// <summary>
    /// Configures the Kernel HTTP pipeline:
    /// maps the kernel OpenAPI JSON document at <c>/openapi/kernel.json</c>.
    /// </summary>
    /// <remarks>
    /// The OpenAPI viewer UI (Scalar or Swagger) is mounted in <c>Program.cs</c> of the Host,
    /// after <c>UseModules()</c>, so it has visibility of all module documents.
    /// Read <c>ApiSettings.OpenApiViewer</c> to determine which viewer to mount.
    /// </remarks>
    public static void UseKernel(this WebApplication app)
    {
        // Expose the OpenAPI JSON documents globally. 
        // In .NET 9, a single call to MapOpenApi() registers the /openapi/{documentName}.json route
        // which dynamically resolves any document added via AddOpenApi(documentName).
        app.MapOpenApi();

        app.MapSettingsEndpoints();
    }

    /// <summary>
    /// Returns the <see cref="ApiSettings"/> registered by <see cref="AddKernel"/>.
    /// Use this in <c>Program.cs</c> to read <c>OpenApiViewer</c> and mount the correct UI.
    /// </summary>
    public static ApiSettings GetApiSettings(this WebApplication app) =>
        app.Services.GetRequiredService<ApiSettings>();
}
