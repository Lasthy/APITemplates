using Kernel.Boxing;
using Kernel.Configuration;
using Kernel.Context;
using Kernel.Events;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Modules;
using Kernel.Partitioning;
using Kernel.Persistence;
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

        // ── Instance settings (loaded from appsettings.Instance.json) ─────────
        builder.Services.AddSingleton<InstanceSettings>(sp =>
        {
            var jsonSerializer = sp.GetRequiredService<IJsonSerializer>();
            var dynamicSettings = new DynamicSettings(jsonSerializer);
            return dynamicSettings.AddSection(new InstanceSettings("appsettings.Instance.json", jsonSerializer));
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
        // Expose the raw kernel OpenAPI JSON document.
        app.MapOpenApi("/openapi/kernel.json")
           .WithGroupName("kernel");
    }

    /// <summary>
    /// Returns the <see cref="ApiSettings"/> registered by <see cref="AddKernel"/>.
    /// Use this in <c>Program.cs</c> to read <c>OpenApiViewer</c> and mount the correct UI.
    /// </summary>
    public static ApiSettings GetApiSettings(this WebApplication app) =>
        app.Services.GetRequiredService<ApiSettings>();
}
