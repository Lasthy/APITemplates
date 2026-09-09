using Kernel.Configuration;
using Kernel.Constants;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Configuration;
using Scheduling.Contracts.Constants;
using Scheduling.Controllers;
using Scheduling.Cron;
using Scheduling.Handlers;
using Scheduling.Handlers.BuiltIn;
using Scheduling.Persistence;
using Scheduling.Services;
using Scheduling.Workers;

namespace Scheduling;

/// <summary>
/// Pluggable module registration for the Scheduling module.
/// Implements <see cref="IModule"/> to integrate with the Kernel module pipeline.
/// </summary>
public class SchedulingModule : IModule
{
    /// <inheritdoc/>
    public static string Name => "Scheduling";

    /// <inheritdoc/>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(SchedulingPolicies.ManageJobs, policy => policy.RequireRole(Roles.System));
    }

    /// <inheritdoc/>
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        // ── Dynamic Settings ───────────────────────────────────────────────────
        var dynamicSettings = services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        SchedulingSettings schedulingSettings;
        if (dynamicSettings != null)
        {
            try
            {
                schedulingSettings = dynamicSettings.GetSection<SchedulingSettings>();
            }
            catch
            {
                var jsonSerializer = new JsonSerializer();
                schedulingSettings = new SchedulingSettings(jsonSerializer, dynamicSettings.Environment);
                dynamicSettings.AddSection(schedulingSettings);
            }
        }
        else
        {
            schedulingSettings = new SchedulingSettings(new JsonSerializer());
        }

        services.AddSingleton(schedulingSettings);

        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<SchedulingModuleDbContext>((sp, options) =>
        {
            var dataSettings = sp.GetService<DataSettings>();
            if (dataSettings != null)
            {
                options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "scheduling");
            }
            else
            {
                options.UseConfiguredDatabase("sqlite", "Data Source=scheduling.db", "scheduling");
            }
            options.AddKernelInterceptors(sp);
        });

        // ── Cron & Registry Services ──────────────────────────────────────────
        services.AddSingleton<ICronParser, CronParser>();

        var registry = new JobHandlerRegistry();
        // Register built-in no-op handler
        registry.Register<NoOpJobHandler>();
        services.AddTransient<NoOpJobHandler>();

        // Scan currently loaded assemblies for IScheduledJobHandler implementations
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            try
            {
                var handlerTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IScheduledJobHandler).IsAssignableFrom(t));

                foreach (var handlerType in handlerTypes)
                {
                    registry.Register(handlerType);
                    services.AddTransient(handlerType);
                }
            }
            catch
            {
                // Ignore type load exceptions in unreferenced assemblies
            }
        }

        services.AddSingleton<IJobHandlerRegistry>(registry);

        // ── Core Engine Services ──────────────────────────────────────────────
        services.AddScoped<IJobLockService, JobLockService>();
        services.AddScoped<IJobScheduler, JobScheduler>();

        // ── Controllers ───────────────────────────────────────────────────────
        services.AddControllers().AddApplicationPart(typeof(JobsController).Assembly);

        // ── Background Workers ────────────────────────────────────────────────
        services.AddHostedService<JobSchedulerWorker>();
        services.AddHostedService<ExecutionCleanupWorker>();
    }

    /// <inheritdoc/>
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via API Controllers
    }
}
