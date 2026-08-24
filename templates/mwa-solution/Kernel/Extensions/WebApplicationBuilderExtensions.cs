using Kernel.Boxing;
using Kernel.Configuration;
using Kernel.Configuration.Controllers;
using Kernel.Configuration.Features;
using Kernel.Context;
using Kernel.Events;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Modules;
using Kernel.Partitioning;
using Kernel.Requests;
using Microsoft.AspNetCore.Builder;
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
    ///   <item><see cref="DynamicSettings"/> — singleton, environment-aware.</item>
    ///   <item><see cref="ApplicationSettings"/> — singleton, loaded from <c>settings.Application.json</c>.</item>
    ///   <item><see cref="InstanceSettings"/> — singleton, loaded from <c>settings.Instance.json</c>.</item>
    ///   <item><see cref="IPartitionManager"/> — singleton, backed by <see cref="PartitionManager"/>.</item>
    ///   <item><see cref="IMessageWriter"/> — scoped, backed by <see cref="MessageWriter"/>.</item>
    ///   <item><see cref="IMessageDispatcher"/> — scoped, backed by <see cref="MessageDispatcher"/>.</item>
    ///   <item><see cref="IMessageProcessor"/> (Event channel) — scoped, backed by <see cref="EventMessageProcessor"/>.</item>
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
        var environment = builder.Environment.EnvironmentName;
        var jsonSerializer = new JsonSerializer();
        var dynamicSettings = new DynamicSettings(jsonSerializer, environment);

        // Register the core settings sections
        var applicationSettings = new ApplicationSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(applicationSettings);

        var instanceSettings = new InstanceSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(instanceSettings);

        builder.Services.AddSingleton(dynamicSettings);
        builder.Services.AddSingleton(applicationSettings);
        builder.Services.AddSingleton(instanceSettings);

        // ── Partition manager ─────────────────────────────────────────────────
        builder.Services.AddSingleton<IPartitionManager, PartitionManager>();

        // ── Messaging / outbox ────────────────────────────────────────────────
        builder.Services.AddScoped<IMessageWriter, MessageWriter>();
        builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        builder.Services.AddScoped<IMessageProcessor, EventMessageProcessor>();

        // ── Controllers ───────────────────────────────────────────────────────
        builder.Services.AddControllers().AddApplicationPart(typeof(SettingsController).Assembly);

        // ── Configuration Features ───────────────────────────────────────────
        builder.Services.AddScoped<IDispatcher, Dispatcher>();
        builder.Services.AddScoped<IRequestHandler<GetSettings, string>, GetSettingsHandler>();
        builder.Services.AddScoped<IRequestHandler<UpdateSettings, bool>, UpdateSettingsHandler>();

        // ── OpenAPI — kernel document ─────────────────────────────────────────
        builder.Services.AddOpenApi("kernel");
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
        var eventBus = app.Services.GetRequiredService<IEventBus>();
        dynamicSettings.SetEventBus(eventBus);

        // Expose the OpenAPI JSON documents globally. 
        // In .NET 9+, a single call to MapOpenApi() registers the /openapi/{documentName}.json route
        // which dynamically resolves any document added via AddOpenApi(documentName).
        app.MapOpenApi();

        app.MapControllers();
    }
}
