using Kernel.Configuration;
using Kernel.Events;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhooks.Configuration;
using Webhooks.Contracts.Constants;
using Webhooks.Controllers;
using Webhooks.Delivery;
using Webhooks.EventBridge;
using Webhooks.Persistence;
using Webhooks.Services;
using Webhooks.Workers;

namespace Webhooks;

/// <summary>
/// Pluggable module registration for the Webhooks module.
/// Implements <see cref="IModule"/> to integrate with the Kernel module pipeline.
/// </summary>
public class WebhooksModule : IModule
{
    /// <inheritdoc/>
    public static string Name => "Webhooks";

    /// <inheritdoc/>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(WebhookPolicies.ManageWebhooks, policy => policy.RequireAuthenticatedUser());
    }

    /// <inheritdoc/>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // ── Dynamic Settings ───────────────────────────────────────────────────
        var dynamicSettings = services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        WebhookSettings webhookSettings;
        if (dynamicSettings != null)
        {
            try
            {
                webhookSettings = dynamicSettings.GetSection<WebhookSettings>();
            }
            catch
            {
                var jsonSerializer = new JsonSerializer();
                webhookSettings = new WebhookSettings(jsonSerializer, dynamicSettings.Environment);
                dynamicSettings.AddSection(webhookSettings);
            }
        }
        else
        {
            webhookSettings = new WebhookSettings(new JsonSerializer());
        }

        services.AddSingleton(webhookSettings);

        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<WebhookModuleDbContext>((sp, options) =>
        {
            var dataSettings = sp.GetService<DataSettings>();
            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "webhooks");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=webhooks.db", "webhooks");
            }
            options.AddKernelInterceptors(sp);
        });

        // ── Delivery & Signature Services ──────────────────────────────────────
        services.AddSingleton<ISignatureService, HmacSignatureService>();
        services.AddHttpClient();
        services.AddScoped<IWebhookDeliveryEngine, WebhookDeliveryEngine>();
        services.AddScoped<IWebhookService, WebhookService>();

        // ── Event Bridge (Captures domain events for fan-out) ─────────────────
        services.AddScoped<IEventHandler<Event>, WebhookEventBridge>();
        services.AddScoped<WebhookEventBridge>();

        // ── Controllers ───────────────────────────────────────────────────────
        services.AddControllers().AddApplicationPart(typeof(SubscriptionsController).Assembly);

        // ── Background Workers ────────────────────────────────────────────────
        services.AddHostedService<WebhookDeliveryWorker>();
        services.AddHostedService<DeliveryCleanupWorker>();
    }

    /// <inheritdoc/>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via API Controllers
    }
}
